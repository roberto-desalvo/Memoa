using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoa.Functions.Data;

/// <summary>
/// Aggiunge la retry logic di SqlClient all'apertura delle connessioni. Copre la ripresa del database
/// serverless, durante la quale l'apertura può andare in timeout (errore -2): il retry di EF Core non
/// lo considera transitorio, perché ritentare un comando andato in timeout non è sicuro. Aprire una
/// connessione invece si può ripetere senza effetti. I comandi restano coperti solo da EF Core.
/// </summary>
public sealed class SqlConnectionRetryInterceptor : DbConnectionInterceptor
{
    private static readonly SqlRetryLogicBaseProvider RetryProvider =
        SqlConfigurableRetryFactory.CreateExponentialRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = 4,
            DeltaTime = TimeSpan.FromSeconds(2),
            MaxTimeInterval = TimeSpan.FromSeconds(20),
            TransientErrors =
            [
                -2,    // Timeout scaduto (database in ripresa)
                20,    // Istanza non disponibile
                64,    // Connessione chiusa durante il login
                233,   // Nessun processo dall'altra parte della pipe
                4060,  // Database non apribile
                10053, // Connessione interrotta
                10054, // Connessione chiusa dall'host remoto
                10060, // Timeout di rete
                40197, // Errore del servizio, riprovare
                40501, // Servizio occupato
                40613, // Database non disponibile (in ripresa)
                49918, // Risorse insufficienti
                49919,
                49920,
            ],
        });

    public override DbConnection ConnectionCreated(ConnectionCreatedEventData eventData, DbConnection result)
    {
        if (result is SqlConnection connection)
        {
            connection.RetryLogicProvider = RetryProvider;
        }

        return result;
    }
}
