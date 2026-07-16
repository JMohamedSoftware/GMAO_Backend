namespace GMAO.Domain.Enums;

public enum RoleEnum
{
    Admin = 1,
    ResponsableMaintenance = 2,
    ChefEquipe = 3,
    Technicien = 4,
    Production = 5,
    Magasinier = 6
}

public enum CriticiteEquipement
{
    Faible = 1,
    Moyenne = 2,
    Haute = 3,
    Critique = 4
}

public enum EtatEquipement
{
    EnService = 1,
    EnPanne = 2,
    EnMaintenance = 3,
    HorsService = 4,
    EnAttente = 5
}

public enum PrioriteIntervention
{
    Basse = 1,
    Normale = 2,
    Haute = 3,
    Urgente = 4
}

public enum StatutDemande
{
    EnAttente = 1,
    Validee = 2,
    Rejetee = 3,
    TransformeeEnOT = 4
}

public enum TypeMaintenance
{
    Corrective = 1,
    Preventive = 2,
    Predictive = 3,
    Ameliorative = 4,
    SousTraitance = 5
}

public enum StatutOT
{
    Cree = 1,
    Planifie = 2,
    EnCours = 3,
    Suspendu = 4,
    Termine = 5,
    Annule = 6
}

public enum StatutIntervention
{
    NonDemarree = 1,
    EnCours = 2,
    Suspendue = 3,
    Terminee = 4
}

public enum TypeDeclenchement
{
    Periodique = 1,      // Ex: tous les 30 jours
    Compteur = 2,        // Ex: toutes les 500h
    Saisonnier = 3       // Ex: début de campagne
}

public enum TypeMouvementStock
{
    Entree = 1,
    Sortie = 2,
    Retour = 3,
    Casse = 4,
    Inventaire = 5
}

public enum EtatCampagne
{
    Planifiee = 1,
    EnCours = 2,
    Terminee = 3,
    Annulee = 4
}

public enum TypeDocument
{
    Manuel = 1,
    Schema = 2,
    Photo = 3,
    Rapport = 4,
    Contrat = 5,
    Certificat = 6,
    Autre = 7
}

public enum TypeNotification
{
    PreventifEcheance = 1,
    OTEnRetard = 2,
    StockCritique = 3,
    NouvelleDemandeIntervention = 4,
    OTAssigne = 5,
    ControleReglementaire = 6
}
