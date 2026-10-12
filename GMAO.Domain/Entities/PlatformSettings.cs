namespace GMAO.Domain.Entities;

public class PlatformSettings
{
    public int Id { get; set; } = 1;
    public string NomPlateforme { get; set; } = "GMAO SaaS Pro";
    public string VersionPlateforme { get; set; } = "v2.5.4-LTS";
    public string UrlPlateforme { get; set; } = "https://gmao-saas.com";
    public string SupportEmail { get; set; } = "support@gmao-saas.com";
    public string SupportTelephone { get; set; } = "+216 71 889 000";
    public string Devise { get; set; } = "TND (DT)";
    public decimal TauxTvaParDefaut { get; set; } = 19.0m;
    public bool MaintenanceMode { get; set; } = false;
    public bool AllowNewRegistrations { get; set; } = true;
    public int DefaultTrialDays { get; set; } = 14;
    public string SmtpHost { get; set; } = "smtp.mailgun.org";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "postmaster@gmao-saas.com";
    public bool SmtpSsl { get; set; } = true;
    public string StorageProvider { get; set; } = "AWS S3 / Wasabi";
    public bool BackupAuto { get; set; } = true;
    public string BackupFrequence { get; set; } = "Quotidien (02:00 AM)";
    public int RetentionJours { get; set; } = 30;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
