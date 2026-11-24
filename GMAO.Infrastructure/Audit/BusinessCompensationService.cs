using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GMAO.Infrastructure.Audit;

/// <summary>
/// Service d'annulation métier contrôlée (Compensating Business Operations).
/// Ne prétend JAMAIS rembobiner le WAL PostgreSQL : exécute une transaction métier compensatoire
/// strictement encadrée, vérifiant les règles métier, la non-dérive de l'état actuel (Optimistic Drift Check)
/// et l'autorisation SuperAdmin, avec journalisation complète dans l'Audit Trail.
/// </summary>
public class BusinessCompensationService : IBusinessCompensationService
{
    private readonly GmaoDbContext _db;
    private readonly ITenantContextService _tenantContext;
    private readonly ILogger<BusinessCompensationService> _logger;

    private static readonly Dictionary<string, HashSet<string>> AllowedFieldsByTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Equipements"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Designation", "Etat", "Criticite", "Notes", "Marque", "Modele"
        },
        ["OrdresTravail"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Priorite", "Statut", "Description", "Instructions", "TechnicienId", "CoutMainOeuvre", "CoutPieces", "CoutSousTraitance"
        },
        ["DemandesIntervention"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Priorite", "Statut", "Description", "CommentaireRejet"
        },
        ["Pieces"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Designation", "StockMinimum", "StockMaximum", "PrixUnitaire", "Emplacement"
        },
        ["Campagnes"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Nom", "Description"
        }
    };

    public BusinessCompensationService(
        GmaoDbContext db,
        ITenantContextService tenantContext,
        ILogger<BusinessCompensationService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public bool IsEntryCompensable(string entityName, string action, string? changedProperties, bool isCompensated, out string? reason)
    {
        if (isCompensated)
        {
            reason = "Cette opération a déjà fait l'objet d'une compensation.";
            return false;
        }

        if (!string.Equals(action, "UPDATE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(action, "STATUS_CHANGE", StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Seules les modifications contrôlées (UPDATE / STATUS_CHANGE) sont compensables. Action '{action}' non réversible automatiquement.";
            return false;
        }

        if (!AllowedFieldsByTable.TryGetValue(entityName, out var allowedCols))
        {
            reason = $"La table '{entityName}' n'est pas éligible à une compensation automatique sécurisée.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(changedProperties))
        {
            reason = "Aucun champ modifié identifiable pour compensation.";
            return false;
        }

        var cols = changedProperties.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (cols.Length == 0 || cols.Any(c => !allowedCols.Contains(c)))
        {
            reason = "Un ou plusieurs champs modifiés touchent à l'intégrité comptable/sécurité et nécessitent une correction manuelle.";
            return false;
        }

        reason = null;
        return true;
    }

    public async Task<CompensateResultDto> CompensateAsync(long auditEntryId, int superAdminUserId, string superAdminName, CancellationToken ct = default)
    {
        if (!_tenantContext.IsSuperAdmin)
        {
            return new CompensateResultDto
            {
                Success = false,
                HttpStatusCode = 403,
                Message = "Accès refusé : seul un SuperAdmin dûment authentifié peut exécuter une annulation métier contrôlée."
            };
        }

        var auditEntry = await _db.AuditTrailEntries.FirstOrDefaultAsync(a => a.Id == auditEntryId, ct);
        if (auditEntry == null)
        {
            return new CompensateResultDto
            {
                Success = false,
                HttpStatusCode = 404,
                Message = $"Entrée d'audit #{auditEntryId} introuvable."
            };
        }

        if (!IsEntryCompensable(auditEntry.EntityName, auditEntry.Action, auditEntry.ChangedProperties, auditEntry.IsCompensated, out var rejectReason))
        {
            return new CompensateResultDto
            {
                Success = false,
                HttpStatusCode = 422,
                Message = rejectReason ?? "Opération non éligible à une compensation métier."
            };
        }

        var oldMap = DeserializeMap(auditEntry.OldValuesJson);
        var newMap = DeserializeMap(auditEntry.NewValuesJson);
        if (oldMap.Count == 0)
        {
            return new CompensateResultDto
            {
                Success = false,
                HttpStatusCode = 422,
                Message = "Valeurs antérieures manquantes : impossible d'exécuter la compensation."
            };
        }

        if (!int.TryParse(auditEntry.EntityId, out int entityId) || entityId <= 0)
        {
            return new CompensateResultDto
            {
                Success = false,
                HttpStatusCode = 422,
                Message = $"Identifiant d'entité invalide '{auditEntry.EntityId}'."
            };
        }

        // Exécution transactionnelle atomique
        var useTransaction = _db.Database.IsRelational();
        await using var tx = useTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var compensationOutcome = await ApplyCompensatingPatchAsync(auditEntry, entityId, oldMap, newMap, ct);
            if (!compensationOutcome.Success)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return compensationOutcome;
            }

            // Marquer l'entrée originale comme compensée et tracer la nouvelle action
            auditEntry.IsCompensated = true;
            auditEntry.CompensatedAtUtc = DateTime.UtcNow;
            auditEntry.CompensatedByUserId = superAdminUserId;

            var compAudit = new AuditTrailEntry
            {
                UserId = superAdminUserId,
                UserName = superAdminName,
                UserRole = "SuperAdmin",
                SocieteId = auditEntry.SocieteId,
                SocieteName = auditEntry.SocieteName,
                EntityName = auditEntry.EntityName,
                EntityId = auditEntry.EntityId,
                Action = "COMPENSATE",
                BusinessActionLabel = $"Annulation métier contrôlée de l'opération #{auditEntry.Id} ({auditEntry.BusinessActionLabel})",
                TimestampUtc = DateTime.UtcNow,
                OldValuesJson = auditEntry.NewValuesJson,
                NewValuesJson = auditEntry.OldValuesJson,
                ChangedProperties = auditEntry.ChangedProperties,
                Source = "SUPERADMIN_COMPENSATION",
                CompensatedEntryId = auditEntry.Id
            };

            _db.AuditTrailEntries.Add(compAudit);

            _db.JournauxSuperAdmin.Add(new JournalSuperAdmin
            {
                DateHeure = DateTime.UtcNow,
                Niveau = "WARNING",
                Action = "Annulation Métier Contrôlée",
                Categorie = "Gouvernance Données",
                UserId = superAdminUserId,
                UserNom = superAdminName,
                UserRole = "SuperAdmin",
                SocieteNom = auditEntry.SocieteName ?? $"Societe #{auditEntry.SocieteId}",
                Details = $"Compensation réussie sur {auditEntry.EntityName} #{auditEntry.EntityId} (Audit #{auditEntry.Id}). Champs rétablis: {auditEntry.ChangedProperties}",
                Statut = "Succès"
            });

            await _db.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            return new CompensateResultDto
            {
                Success = true,
                HttpStatusCode = 200,
                OriginalAuditEntryId = auditEntry.Id,
                CompensatingAuditEntryId = compAudit.Id,
                Message = $"Annulation métier contrôlée appliquée avec succès sur {auditEntry.EntityName} #{auditEntry.EntityId}."
            };
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Erreur lors de la compensation de l'audit #{AuditId}", auditEntryId);
            return new CompensateResultDto
            {
                Success = false,
                HttpStatusCode = 500,
                Message = $"Erreur technique lors de l'opération compensatoire : {ex.Message}"
            };
        }
    }

    private async Task<CompensateResultDto> ApplyCompensatingPatchAsync(
        AuditTrailEntry audit,
        int entityId,
        Dictionary<string, JsonElement> oldMap,
        Dictionary<string, JsonElement> newMap,
        CancellationToken ct)
    {
        switch (audit.EntityName)
        {
            case "Equipements":
            {
                var eq = await _db.Equipements.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == entityId, ct);
                if (eq == null) return Fail(404, "Équipement introuvable (probablement supprimé).");

                if (!CheckDrift(eq.Designation, "Designation", newMap) ||
                    !CheckDrift(eq.Etat.ToString(), "Etat", newMap) ||
                    !CheckDrift(eq.Criticite.ToString(), "Criticite", newMap) ||
                    !CheckDrift(eq.Notes, "Notes", newMap) ||
                    !CheckDrift(eq.Marque, "Marque", newMap) ||
                    !CheckDrift(eq.Modele, "Modele", newMap))
                {
                    return Fail(409, "Conflit d'état : l'équipement a été modifié ultérieurement par une autre transaction.");
                }

                if (oldMap.TryGetValue("Designation", out var d)) eq.Designation = d.GetString() ?? eq.Designation;
                if (oldMap.TryGetValue("Notes", out var n)) eq.Notes = n.ValueKind == JsonValueKind.Null ? null : n.GetString();
                if (oldMap.TryGetValue("Marque", out var m)) eq.Marque = m.ValueKind == JsonValueKind.Null ? null : m.GetString();
                if (oldMap.TryGetValue("Modele", out var mo)) eq.Modele = mo.ValueKind == JsonValueKind.Null ? null : mo.GetString();
                if (oldMap.TryGetValue("Etat", out var et) && TryParseEnum<EtatEquipement>(et, out var oldEtat)) eq.Etat = oldEtat;
                if (oldMap.TryGetValue("Criticite", out var cr) && TryParseEnum<CriticiteEquipement>(cr, out var oldCrit)) eq.Criticite = oldCrit;
                eq.UpdatedAt = DateTime.UtcNow;
                return Ok();
            }

            case "OrdresTravail":
            {
                var ot = await _db.OrdresTravail.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == entityId, ct);
                if (ot == null) return Fail(404, "Ordre de travail introuvable.");

                if (ot.Statut == StatutOT.Cloture || ot.Statut == StatutOT.Annule)
                    return Fail(409, "Règle métier : impossible de compenser un ordre de travail clôturé ou annulé.");

                if (!CheckDrift(ot.Priorite.ToString(), "Priorite", newMap) ||
                    !CheckDrift(ot.Statut.ToString(), "Statut", newMap) ||
                    !CheckDrift(ot.Description, "Description", newMap) ||
                    !CheckDrift(ot.Instructions, "Instructions", newMap) ||
                    !CheckDrift(ot.TechnicienId, "TechnicienId", newMap))
                {
                    return Fail(409, "Conflit d'état : l'ordre de travail a été modifié ultérieurement.");
                }

                if (oldMap.TryGetValue("Priorite", out var pr) && TryParseEnum<PrioriteIntervention>(pr, out var oldPri)) ot.Priorite = oldPri;
                if (oldMap.TryGetValue("Statut", out var st) && TryParseEnum<StatutOT>(st, out var oldStatut))
                {
                    if (oldStatut == StatutOT.Cloture || oldStatut == StatutOT.Annule)
                        return Fail(409, "Transition compensatoire interdite vers un état terminal.");
                    ot.Statut = oldStatut;
                }
                if (oldMap.TryGetValue("Description", out var desc)) ot.Description = desc.GetString() ?? ot.Description;
                if (oldMap.TryGetValue("Instructions", out var inst)) ot.Instructions = inst.ValueKind == JsonValueKind.Null ? null : inst.GetString();
                if (oldMap.TryGetValue("TechnicienId", out var tid)) ot.TechnicienId = TryGetNullableInt(tid);
                if (oldMap.TryGetValue("CoutMainOeuvre", out var cmo)) ot.CoutMainOeuvre = TryGetNullableDecimal(cmo);
                if (oldMap.TryGetValue("CoutPieces", out var cp)) ot.CoutPieces = TryGetNullableDecimal(cp);
                if (oldMap.TryGetValue("CoutSousTraitance", out var cst)) ot.CoutSousTraitance = TryGetNullableDecimal(cst);
                return Ok();
            }

            case "DemandesIntervention":
            {
                var inc = await _db.DemandesIntervention.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == entityId, ct);
                if (inc == null) return Fail(404, "Demande d'intervention introuvable.");

                if (inc.Statut == StatutDemande.TransformeeEnOT || inc.Statut == StatutDemande.Cloturee)
                    return Fail(409, "Règle métier : impossible de compenser une demande déjà transformée en OT ou clôturée.");

                if (!CheckDrift(inc.Priorite.ToString(), "Priorite", newMap) ||
                    !CheckDrift(inc.Statut.ToString(), "Statut", newMap) ||
                    !CheckDrift(inc.Description, "Description", newMap) ||
                    !CheckDrift(inc.CommentaireRejet, "CommentaireRejet", newMap))
                {
                    return Fail(409, "Conflit d'état : l'incident a été modifié ultérieurement.");
                }

                if (oldMap.TryGetValue("Priorite", out var pr) && TryParseEnum<PrioriteIntervention>(pr, out var oldPri)) inc.Priorite = oldPri;
                if (oldMap.TryGetValue("Statut", out var st) && TryParseEnum<StatutDemande>(st, out var oldStatut)) inc.Statut = oldStatut;
                if (oldMap.TryGetValue("Description", out var desc)) inc.Description = desc.GetString() ?? inc.Description;
                if (oldMap.TryGetValue("CommentaireRejet", out var cr)) inc.CommentaireRejet = cr.ValueKind == JsonValueKind.Null ? null : cr.GetString();
                return Ok();
            }

            case "Pieces":
            {
                var piece = await _db.Pieces.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == entityId, ct);
                if (piece == null) return Fail(404, "Pièce introuvable.");

                if (!CheckDrift(piece.Designation, "Designation", newMap) ||
                    !CheckDrift(piece.StockMinimum, "StockMinimum", newMap) ||
                    !CheckDrift(piece.StockMaximum, "StockMaximum", newMap) ||
                    !CheckDrift(piece.PrixUnitaire, "PrixUnitaire", newMap) ||
                    !CheckDrift(piece.Emplacement, "Emplacement", newMap))
                {
                    return Fail(409, "Conflit d'état : la pièce a été modifiée ultérieurement.");
                }

                if (oldMap.TryGetValue("Designation", out var d)) piece.Designation = d.GetString() ?? piece.Designation;
                if (oldMap.TryGetValue("StockMinimum", out var smin) && TryGetDecimal(smin, out var minVal)) piece.StockMinimum = minVal;
                if (oldMap.TryGetValue("StockMaximum", out var smax) && TryGetDecimal(smax, out var maxVal)) piece.StockMaximum = maxVal;
                if (oldMap.TryGetValue("PrixUnitaire", out var pu) && TryGetDecimal(pu, out var puVal)) piece.PrixUnitaire = puVal;
                if (oldMap.TryGetValue("Emplacement", out var emp)) piece.Emplacement = emp.ValueKind == JsonValueKind.Null ? null : emp.GetString();
                piece.UpdatedAt = DateTime.UtcNow;
                return Ok();
            }

            case "Campagnes":
            {
                var camp = await _db.Campagnes.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == entityId, ct);
                if (camp == null) return Fail(404, "Campagne introuvable.");

                if (!CheckDrift(camp.Nom, "Nom", newMap) || !CheckDrift(camp.Description, "Description", newMap))
                    return Fail(409, "Conflit d'état : la campagne a été modifiée ultérieurement.");

                if (oldMap.TryGetValue("Nom", out var nom)) camp.Nom = nom.GetString() ?? camp.Nom;
                if (oldMap.TryGetValue("Description", out var desc)) camp.Description = desc.ValueKind == JsonValueKind.Null ? null : desc.GetString();
                return Ok();
            }

            default:
                return Fail(422, "Entité non supportée pour la compensation.");
        }
    }

    private static CompensateResultDto Ok() => new() { Success = true, HttpStatusCode = 200 };
    private static CompensateResultDto Fail(int code, string msg) => new() { Success = false, HttpStatusCode = code, Message = msg };

    private static Dictionary<string, JsonElement> DeserializeMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static bool CheckDrift(object? currentVal, string key, Dictionary<string, JsonElement> newMap)
    {
        if (!newMap.TryGetValue(key, out var expectedEl)) return true;

        if (expectedEl.ValueKind == JsonValueKind.Null)
            return currentVal == null;

        if (currentVal == null) return false;

        if (currentVal is decimal curDec && TryGetDecimal(expectedEl, out var expDec))
            return curDec == expDec;

        if (currentVal is int curInt && expectedEl.ValueKind == JsonValueKind.Number && expectedEl.TryGetInt32(out var expInt))
            return curInt == expInt;

        var expStr = expectedEl.ValueKind == JsonValueKind.String ? expectedEl.GetString() : expectedEl.ToString();
        return string.Equals(currentVal.ToString(), expStr, StringComparison.Ordinal);
    }

    private static bool TryParseEnum<TEnum>(JsonElement el, out TEnum value) where TEnum : struct, Enum
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int num))
        {
            value = (TEnum)(object)num;
            return Enum.IsDefined(typeof(TEnum), value);
        }
        if (el.ValueKind == JsonValueKind.String)
        {
            return Enum.TryParse(el.GetString(), ignoreCase: true, out value);
        }
        value = default;
        return false;
    }

    private static int? TryGetNullableInt(JsonElement el) =>
        el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int v) ? v : null;

    private static decimal? TryGetNullableDecimal(JsonElement el) =>
        TryGetDecimal(el, out var d) ? d : null;

    private static bool TryGetDecimal(JsonElement el, out decimal value)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out value)) return true;
        if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out value)) return true;
        value = 0;
        return false;
    }
}
