namespace GMAO.Infrastructure.Cdc;

public class PostgresCdcOptions
{
    public const string SectionName = "CdcSettings";

    /// <summary>
    /// Active ou désactive le worker d'ingestion WAL / CDC en arrière-plan.
    /// Par défaut à false tant qu'un endpoint de réplication directe (hors pooler Supavisor)
    /// avec wal_level=logical et connectivité réseau directe (IPv4 Add-on ou IPv6) n'est pas provisionné.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Chaîne de connexion PostgreSQL DIRECTE (port 5432, jamais via Supavisor pooler).
    /// Doit pointer vers un rôle disposant de l'attribut REPLICATION.
    /// </summary>
    public string? ReplicationConnectionString { get; set; }

    public string SlotName { get; set; } = "gmao_cdc_wal_slot";
    public string PublicationName { get; set; } = "gmao_cdc_pub";
    public int ReconnectDelaySeconds { get; set; } = 15;
    public int ProbeIntervalSeconds { get; set; } = 60;
    public long SlotLagWarningBytes { get; set; } = 50L * 1024 * 1024; // 50 Mo
}
