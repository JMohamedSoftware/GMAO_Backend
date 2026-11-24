namespace GMAO.Infrastructure.Storage;

public class SupabaseStorageOptions
{
    public const string SectionName = "Supabase";

    /// <summary>
    /// URL du projet Supabase (ex: https://rbolwywlpzidkbrczcww.supabase.co).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Clé secrète service_role (injectée exclusivement via variable d'environnement Backend Render).
    /// Ne JAMAIS exposer dans React, Git ou les réponses HTTP.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    /// <summary>
    /// Nom du bucket privé Supabase Storage.
    /// </summary>
    public string BucketName { get; set; } = "gmao-documents";

    /// <summary>
    /// Durée de validité par défaut des Signed URLs temporaires (en secondes).
    /// </summary>
    public int SignedUrlExpiresSeconds { get; set; } = 120;

    /// <summary>
    /// Mode de routage : "Auto" (Supabase si configuré, sinon Local), "Supabase" (exige Supabase), ou "Local".
    /// </summary>
    public string ProviderMode { get; set; } = "Auto";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Url) &&
        !Url.Contains("REPLACED_BY_ENV_VAR", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(ServiceRoleKey) &&
        !ServiceRoleKey.Contains("REPLACED_BY_ENV_VAR", StringComparison.OrdinalIgnoreCase);
}
