using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Data;

/// <summary>
/// Connessione ad Azure SQL (sezione "Sql"). Sempre autenticazione Entra ID, mai password: in Azure
/// "Active Directory Managed Identity" con User Id = client ID dell'identità user-assigned, in locale
/// "Active Directory Default" (utente di Azure CLI o Visual Studio).
/// </summary>
public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
