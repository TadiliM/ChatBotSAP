using System.Security.Cryptography;
using Npgsql; 
using Microsoft.Data.SqlClient;

/***
 * Programme .NET pour ajouter des informations clients (numéro et connection au SQL Server SAP)
 * de façon sécurisée pour la base de données des clients SAP.
 *
 * Usage : dotnet run Program.cs <number> <serverUrl> <dbName>
 */


/*—————————————————————————————————————————————————————————*/
/* Gestion d'encryptage (Pris de la doc Microsoft) */
/*—————————————————————————————————————————————————————————*/

static string EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
{
    // Check arguments.
    if (plainText == null || plainText.Length <= 0)
        throw new ArgumentNullException("plainText");
    if (Key == null || Key.Length <= 0)
        throw new ArgumentNullException("Key");
    if (IV == null || IV.Length <= 0)
        throw new ArgumentNullException("IV");
    byte[] encrypted;

    // Create an Aes object
    // with the specified key and IV.
    using (Aes aesAlg = Aes.Create())
    {
        aesAlg.Key = Key;
        aesAlg.IV = IV;

        // Create an encryptor to perform the stream transform.
        ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

        // Create the streams used for encryption.
        using (MemoryStream msEncrypt = new MemoryStream())
        {
            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            {
                using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                {
                    //Write all data to the stream.
                    swEncrypt.Write(plainText);
                }
            }

            encrypted = msEncrypt.ToArray();
        }
    }

    // Return the encrypted bytes from the memory stream.
    return Convert.ToBase64String(encrypted);
}

/*—————————————————————————————————————————————————————————*/

static string GeneratePassword(int length = 12)
{
    string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#%?_-";
    char[] passwordChars = new char[length];

    for (int i = 0; i < length; i++)
    {
        int randomIndex = RandomNumberGenerator.GetInt32(allowedChars.Length);
        passwordChars[i] = allowedChars[randomIndex];
    }

    // Conversion finale du tableau en string
    return new string(passwordChars);
}


string GetNumber(string numberTest = null)
{
    string number;
    if (!string.IsNullOrEmpty(numberTest))
    {
        number = numberTest;
    }
    else
    {
        Console.WriteLine("Veuillez entrer le numéro de téléphone client : ");
        number = Console.ReadLine();
    }
    
    while (!long.TryParse(number, out long numberLong))
    {
        Console.WriteLine("Veuillez entrer un numéro au format valide (SSXXXXXXXXX ; S : indicateur pays , X : chiffre) :\n");
        number = Console.ReadLine();
    }

    return number;
}

string GetServer(string serverTest = null)
{
    string server;
    if (!string.IsNullOrEmpty(serverTest))
    {
        server = serverTest;
    }
    else
    {
        Console.WriteLine("Veuillez entrer le host, adresse IP ou endpoint URL du server : ");
        server = Console.ReadLine();
        
    }

    while (server.Equals(""))
    {
        Console.WriteLine("Veuillez entrer une URL ou adresse IP pour le serveur :\n");
        server = Console.ReadLine();
    }

    return server;
}

string GetDatabase(string dbTest = null)
{
    string db;
    if (!string.IsNullOrEmpty(dbTest))
    {
        db = dbTest;
    }
    else
    {
        Console.WriteLine("Veuillez entrer le nom de la base de données : ");
        db = Console.ReadLine();
        
    }
    
    while (db.Equals(""))
    {
        Console.WriteLine("Veuillez entrer le nom de la base de données :\n");
        db = Console.ReadLine();
    }

    return db;
}

string GetAdminPassword()
{
    Console.WriteLine("Veuillez entrer le mot de passe du compte admin de la base de données : ");
    
    var password = string.Empty;
    ConsoleKey key;
    do
    {
        var keyInfo = Console.ReadKey(intercept: true);
        key = keyInfo.Key;

        if (key == ConsoleKey.Backspace && password.Length > 0)
        {
            Console.Write("\b \b");
            password = password[0..^1];
        }
        else if (!char.IsControl(keyInfo.KeyChar))
        {
            Console.Write("*");
            password += keyInfo.KeyChar;
        }
    } while (key != ConsoleKey.Enter);


    return password;
}



/*  Fonction pour essayer la connection avec le SQL Server  */
static bool IsServerConnected(string connectionString)
{
    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        try
        {
            connection.Open();
            Console.WriteLine("\n \nConnection réussie au SQL Server.\n");
            return true;
        }
        catch (SqlException e)
        {
            Console.WriteLine("\nErreur dans la connection au server avec les informations fournies :\n");
            Console.WriteLine(e.Message);
            return false;
        }
    }
}



if (args.Length != 4)
{
    Console.WriteLine($"Usage : dotnet run {args[0]} <number> <dbName>\n");
}

var numberArg = args.Length > 1 ? args[1] : null;
var servArg   = args.Length > 2 ? args[2] : null;
var dbArg     = args.Length > 3 ? args[3] : null;
    
var number  = GetNumber(numberArg);
var server  = GetServer(servArg);
var db      = GetDatabase(dbArg);
var password= GetAdminPassword();


string connectionStringAdmin = "Server=" + server + ";Database=" + db + ";User Id=sa;Password=" + password +
                          ";Encrypt=True;TrustServerCertificate=True;";



if (!IsServerConnected(connectionStringAdmin))
{
    Environment.Exit(1);
}


/* Ici, connectionString est valide. On le chiffre et l'enregistre. */
DotNetEnv.Env.Load();

var aes_keyString = Environment.GetEnvironmentVariable("AES_192_KEY");
var aes_key = Convert.FromHexString(aes_keyString);

var connectionStringUser = Environment.GetEnvironmentVariable("CONNECTION_STRING_SAP");

var username = "chatbot_user";
var passwordUser = GeneratePassword(20);


/* Création du compte SQL Server restreint */
using (SqlConnection connection = new SqlConnection(connectionStringAdmin))
{
    string query =
        $"IF NOT Exists (SELECT * from sys.server_principals where name = 'chatbot_login_{number}')" +
        $"  BEGIN" +
        $"    CREATE LOGIN chatbot_login_{number} WITH PASSWORD = '{passwordUser}';" +
        $"END;" +
        $"" +
        $"IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'chatbot_user')" +
        $"BEGIN" +
        $"    CREATE USER chatbot_user FOR LOGIN chatbot_login_{number};" +
        $"END;" +
        $"GRANT SELECT ON SCHEMA::dbo TO chatbot_user;";

    SqlCommand command = new SqlCommand(query, connection);
    
    
    connection.Open();
    command.ExecuteNonQuery();
}


var connectionString = $"Server={server};Database={db};User Id=chatbot_login_{number};Password={passwordUser};Encrypt=True;TrustServerCertificate=True;";



/* Vérification si le numéro est déjà enregistré */
using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringUser))
{
    string queryTestNumber = "SELECT * FROM users_sap WHERE number = @num";
    NpgsqlCommand commandTestNumber = new NpgsqlCommand(queryTestNumber, connection);
    commandTestNumber.Parameters.AddWithValue("@num", number);
    
    connection.Open();
    NpgsqlDataReader reader = commandTestNumber.ExecuteReader();

    if (reader.HasRows)
    {
        Console.WriteLine($"\n /!\\ Le numéro {number} est déja enregistrée dans la base clients avec une base SAP.");
        Environment.Exit(3);
    }
    
}




using (Aes aes = Aes.Create())
{
    var encryptedContent = EncryptStringToBytes_Aes(connectionString, aes_key, aes.IV);
    
    string content = Convert.ToBase64String(aes.IV) + ":" + encryptedContent;
    
    using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringUser))
    {
        string query = "INSERT INTO users_sap (number, db_connection ) VALUES (@num, @content);";
        NpgsqlCommand command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@content",  content);
        command.Parameters.AddWithValue("@num",  number);

        
        
        try
        {
            connection.Open();
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Console.WriteLine("Erreur dans l'enregistrement du client dans la base des clients :\n");
            Console.WriteLine(e.Message);
            Environment.Exit(2);
        }
            
    }

}


Console.WriteLine("\nLa base SQL Server a bien été ajoutée à la base client.");


















