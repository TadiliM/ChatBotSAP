using System.Text.Json;
using Microsoft.Data.SqlClient;
using Npgsql; 
using System.Security.Cryptography;



/*—————————————————————————————————————————————————————————*/
/* Gestion d'encryptage (Pris de la doc Microsoft) */
/*—————————————————————————————————————————————————————————*/

static string DecryptStringFromBytes_Aes(string cipherTextString, byte[] key, byte[] iv)
{
    // Check arguments.
    if (cipherTextString == null || cipherTextString.Length <= 0)
        throw new ArgumentNullException("cipherText");
    if (key == null || key.Length <= 0)
        throw new ArgumentNullException("Key");
    if (iv == null || iv.Length <= 0)
        throw new ArgumentNullException("IV");

    
    byte[] cipherText = Convert.FromBase64String(cipherTextString); 
    
    // Declare the string used to hold
    // the decrypted text.
    string plaintext = null;

    // Create an Aes object
    // with the specified key and IV.
    using (Aes aesAlg = Aes.Create())
    {
        aesAlg.Key = key;
        aesAlg.IV = iv;

        // Create a decryptor to perform the stream transform.
        ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

        // Create the streams used for decryption.
        using (MemoryStream msDecrypt = new MemoryStream(cipherText))
        {
            using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
            {
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {

                    // Read the decrypted bytes from the decrypting stream
                    // and place them in a string.
                    plaintext = srDecrypt.ReadToEnd();
                }
            }
        }
    }

    return plaintext;
}

/*—————————————————————————————————————————————————————————*/









var builder = WebApplication.CreateBuilder(args);
DotNetEnv.Env.Load();
builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();







/* —— ConnectionString pour obtenir l'accès à la DB selon l'utilisateur —— */
string connectionStringUser = app.Configuration["CONNECTION_STRING_SAP"];

/* —— AES KEY pour l'encryptage                                         —— */
var aes_keyString                   = app.Configuration["AES_192_KEY"];
var aes_key = Convert.FromHexString(aes_keyString);





/* [GetDbFromNumber] prend la connectionString associé à la base SAP associé au numéro [number]
 * return "not found" si le numéro n'est pas enregistré.
 *
 * */
async Task<string> GetDbFromNumber(string num)
{
    string connectionstring = "not found";
    
    using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringUser))
    {
        string query = "SELECT db_connection FROM users_sap WHERE number = @num";
        NpgsqlCommand command = new NpgsqlCommand(query,connection);
        command.Parameters.AddWithValue("@num", num);
        connection.Open();
        NpgsqlDataReader reader = command.ExecuteReader();

        try
        {
            while (reader.Read())
            {
                var encryptedContent = reader.GetFieldValue<string>(0);
                
                if (encryptedContent != "")
                {
                    /*IV*/
                    var previousIvString = encryptedContent.Split(":")[0];
                    var previousIv = Convert.FromBase64String(previousIvString);
                
                    /*connectionString*/
                    var previousMsg = encryptedContent.Split(":")[1];
                    var previousContent = DecryptStringFromBytes_Aes(previousMsg, aes_key, previousIv);
                    
                    connectionstring = previousContent;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally{reader.Close();}
    }
    
    return connectionstring;
}



/* [IsQuerySafe] vérifie que la requête envoyée au serveur ne contient aucune modification ou suppression
 * de la base de données.
 * */
bool IsQuerySafe(string query)
{
    string[] forbiddenCommand = {"DELETE", "UPDATE", "ALTER", "DROP", "INSERT","CREATE","EXEC","TRUNCATE","MERGE"};
    query = query.ToUpper();
    
    if (forbiddenCommand.Any(s => query.Contains(s)))
    {
        return false;
    }
    return true;
}


/* Envoyer un JSON avec {"query":"SELECT... ;", "number":"212717..."} */

app.MapPost("/query", async (HttpRequest req) =>
{
    /* Vérification du token requête http */
    var tokenQuery = app.Configuration["TOKEN_QUERY"];
    var tokenRequest = req.Query["token"];

    if (tokenRequest != tokenQuery)
    {
        return Results.StatusCode(401);
    }

    
    
    
    /* Extraction de la requête SQL */
    using var stream = new StreamReader(req.Body);
    string body = await stream.ReadToEndAsync();
    var json = JsonDocument.Parse(body);

    string requeteSQL;
    string numuUser;
    
    try
    {
        requeteSQL = json.RootElement.GetProperty("query").ToString();
        numuUser = json.RootElement.GetProperty("number").ToString();

    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        return Results.BadRequest("JSON Format invalid");
    }
    
    
    /* Vérification de la sureté de la requête */
    requeteSQL = requeteSQL.Split(";")[0];
    
    if (!IsQuerySafe(requeteSQL))
    {
        return Results.StatusCode(403);
    }
    
    /* Récupération de la connectionString */
    string connectionStringSAP = await GetDbFromNumber(numuUser);

    if (connectionStringSAP == "not found")
    {
        return Results.NotFound();
    }
    
    Console.WriteLine($"Got : \nnumber : {numuUser}\nSQL :{requeteSQL}\nConnection : {connectionStringSAP}");

    /* Tableau du résultat */
    var tableau = new List<object[]>();
    
    Console.WriteLine("—> Received : (From : " + numuUser + " )\n" + requeteSQL + "\n");

    try
    {

        using (SqlConnection connection = new SqlConnection(connectionStringSAP))
        {
            /* Execution de la requête SQL */
            SqlCommand command = new SqlCommand(requeteSQL, connection);
            command.CommandTimeout = 5;
            connection.Open();
            SqlDataReader reader = command.ExecuteReader();



            try
            {
                while (reader.Read())
                {
                    var ligne = new object[reader.FieldCount];
                    reader.GetValues(ligne);
                    tableau.Add(ligne);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
            finally
            {
                reader.Close();
            }

            connection.Close();
        }
        Console.WriteLine("\n———FINISH———\n");
        return Results.Ok(tableau);

    }
    catch (Exception e)
    {
        Console.WriteLine("————————————————\nError : \n" + e.Message + "\n———————————————\n");
        return Results.BadRequest(e.Message);
        
    }
    
}
    
);

app.Run();
