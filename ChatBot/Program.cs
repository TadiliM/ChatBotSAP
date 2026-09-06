using System.Net;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Npgsql;
using System.Security.Cryptography;

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


static string DecryptStringFromBytes_Aes(string cipherTextString, byte[] Key, byte[] IV)
{
    // Check arguments.
    if (cipherTextString == null || cipherTextString.Length <= 0)
        throw new ArgumentNullException("cipherText");
    if (Key == null || Key.Length <= 0)
        throw new ArgumentNullException("Key");
    if (IV == null || IV.Length <= 0)
        throw new ArgumentNullException("IV");

    
    byte[] cipherText = Convert.FromBase64String(cipherTextString); 
    
    // Declare the string used to hold
    // the decrypted text.
    string plaintext = null;

    // Create an Aes object
    // with the specified key and IV.
    using (Aes aesAlg = Aes.Create())
    {
        aesAlg.Key = Key;
        aesAlg.IV = IV;

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


/* Obtention des variables d'environnements */
DotNetEnv.Env.Load();
builder.Configuration.AddEnvironmentVariables();


var app = builder.Build();


var urlLlmDiscussion          = app.Configuration["URL_API_LLM_DISCUSSION"];
var llmApiKeyDiscussion       = app.Configuration["LLM_DISCUSSION_API_KEY"];
var modelLlmDiscussion        = app.Configuration["MODEL_LLM_DISCUSSION"];
     
var urlLlmFormat              = app.Configuration["URL_API_LLM_FORMAT"];
var llmApiKeyFormat           = app.Configuration["LLM_FORMAT_API_KEY"];
var modelLlmFormat            = app.Configuration["MODEL_LLM_FORMAT"];
     
     
var evolutionApiKey           = app.Configuration["EVOLUTION_API_KEY"];
var evolutionUrlMessage             = app.Configuration["EVOLUTION_URL_MESSAGE"];
var evolutionUrlBase64        = app.Configuration["EVOLUTION_URL_BASE_64"];
     
var sapUrlServer              = app.Configuration["SAP_QUERY_SERVICE"];
     
var transcriptionApiKey       = app.Configuration["TRANSCRIPTION_API_KEY"];
var transcriptionModel        = app.Configuration["TRANSCRIPTION_MODEL"];
var transcriptionApiUrl       = app.Configuration["TRANSCRIPTION_API_URL"];
     
var tokenQuery                = app.Configuration["TOKEN_QUERY"];

var connectionStringConversation    = app.Configuration["CONNECTION_STRING_CONVERSATIONS"];

var aes_keyString                   = app.Configuration["AES_192_KEY"];
var aes_key = Convert.FromHexString(aes_keyString);

//var promptSystemSapQuery = app.Configuration["PROMPT_SYSTEM_SAP_QUERY"];          
//var promptSystemFormat   = app.Configuration["PROMPT_SYSTEM_FORMAT"];
var promptSystemSapQuery =
    "Tu es un expert SAP Business One. Ton rôle est de traduire les demandes utilisateurs en requêtes SQL pour une base SAP Business One sur SQL Server.\n\nRÈGLES ABSOLUES :\n- Retourner TOUJOURS et UNIQUEMENT un JSON valide de cette forme exacte : {\"query\":\"...\",\"response\":\"...\"}\n- Aucun texte avant ou après le JSON. Aucun markdown. Aucun commentaire. Aucun bloc de code. Pas de ```json```.\n- Si la demande nécessite des données SAP : {\"query\":\"SELECT ...;\",\"response\":\"\"}\n- Si la demande est une conversation normale ou non liée à SAP : {\"query\":\"\",\"response\":\"ta réponse ici\"}\n-Ne remplit seulement QUE \"query\" ou \"response\" dans ton json. JAMAIS les 2.\n-Ne renvoie JAMAIS de réponse vide. Si tu n'a rien a dire, renvoi un \"response\" avec un message court dans ton json.\n- Générer UNIQUEMENT des SELECT. Jamais INSERT, UPDATE, DELETE, DROP, TRUNCATE, EXEC.\n- Ne jamais inventer des tables ou colonnes absentes du schéma.\n- JOIN toujours via DocEntry, jamais DocNum.\n- DocNum uniquement pour l'affichage dans le SELECT.\n- OINV = factures vente, ORDR = commandes vente, OPCH = factures achat, OPOR = commandes achat.\n\nSCHÉMA SAP B1 :\nOINV:DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,VatSum,DocStatus,Comments,SlpCode\nINV1:DocEntry,LineNum,ItemCode,Dscription,Quantity,Price,LineTotal,WhsCode,LineStatus\nORDR:DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,DocStatus,Comments,SlpCode\nRDR1:DocEntry,LineNum,ItemCode,Dscription,Quantity,Price,LineTotal,WhsCode\nODLN:DocEntry,DocNum,CardCode,CardName,DocDate,DocTotal,DocStatus,Comments\nDLN1:DocEntry,LineNum,ItemCode,Dscription,Quantity,Price,LineTotal,WhsCode\nOPOR:DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,DocStatus,Comments\nPOR1:DocEntry,LineNum,ItemCode,Dscription,Quantity,Price,LineTotal,WhsCode\nOPCH:DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,VatSum,DocStatus,Comments\nPCH1:DocEntry,LineNum,ItemCode,Dscription,Quantity,Price,LineTotal,WhsCode\nOCRD:CardCode,CardName,CardType,Balance,CreditLine,Phone1,E_Mail,City,Country,SlpCode\nOITM:ItemCode,ItemName,OnHand,IsCommited,OnOrder,AvgPrice,ItemType,InvntryUom\nOITW:ItemCode,WhsCode,OnHand,IsCommited,OnOrder\nOINM:ItemCode,DocDate,InQty,OutQty,TransType,WhsCode,CalcPrice\nOWHS:WhsCode,WhsName,Location\nORCT:DocEntry,DocNum,CardCode,DocDate,DocTotal,Comments\nOJDT:TransId,RefDate,Memo,TransType,CreatedBy\nOADM:SysCurr,CompnyName,MainCurncy\nJDT1:TransId,Account,Debit,Credit,LineMemo,CostingCode\n\nJOINTURES :\nOINV.DocEntry = INV1.DocEntry\nORDR.DocEntry = RDR1.DocEntry\nODLN.DocEntry = DLN1.DocEntry\nOPOR.DocEntry = POR1.DocEntry\nOPCH.DocEntry = PCH1.DocEntry\nOJDT.TransId = JDT1.TransId\nOINV.CardCode = OCRD.CardCode\nORDR.CardCode = OCRD.CardCode\nINV1.ItemCode = OITM.ItemCode\nOITW.ItemCode = OITM.ItemCode\nOITW.WhsCode = OWHS.WhsCode"; 
var promptSystemFormat = "Tu es un assistant intelligent connecté à SAP Business One. Tu reçois des données brutes extraites de la base SAP et tu dois formuler une réponse claire et concise pour WhatsApp.\n\nRÈGLES DE FORMATAGE :\n- Réponse courte et lisible sur mobile\n- Utiliser des emojis pertinents pour améliorer la lisibilité\n- Utiliser des tirets pour les listes, jamais de markdown (pas de **, pas de #)\n- Les montants toujours avec la devise (ex: 15 000 EUR)\n- Les dates au format JJ/MM/AAAA\n- Maximum 10 éléments dans une liste, résumer le reste (ex: \"... et 5 autres\")\n- Si les données sont vides, répondre poliment que rien n'a été trouvé\n- Ne jamais exposer les noms techniques SAP (DocEntry, CardCode, etc.) dans la réponse\n- Répondre dans la même langue que la question de l'utilisateur\n\nEXEMPLES DE FORMATAGE :\n\nFactures impayées :\n📄 Factures impayées (3)\n- Facture #1042 | Client ABC | 12 500 EUR | échéance 15/05/2026\n- Facture #1043 | Société XYZ | 8 200 EUR | échéance 20/05/2026\n- Facture #1044 | Jean Dupont | 3 100 EUR | échéance 22/05/2026\nTotal : 23 800 EUR\n\nStock article :\n📦 Stock - Ordinateur portable\n- Disponible : 12 unités\n- Réservé : 3 unités\n- En commande : 5 unités\n\nChiffre d'affaires :\n📊 Chiffre d'affaires - Mai 2026\n- Total facturé : 145 000 EUR\n- Nombre de factures : 8\n- Meilleur client : Entreprise ABC (45 000 EUR)\n\nAucun résultat :\n😕 Aucun résultat trouvé pour votre demande. Pouvez-vous reformuler ?";

/*—————————————————————————————————————————————————————————*/
/************* Définition des clients Http *****************/
/*—————————————————————————————————————————————————————————*/

var clientLlmDiscussion = new HttpClient() {BaseAddress = new Uri(urlLlmDiscussion)};
clientLlmDiscussion.DefaultRequestHeaders.Add("Authorization","Bearer " + llmApiKeyDiscussion);


var clientLlmFormat = new HttpClient() { BaseAddress = new Uri(urlLlmFormat) };
clientLlmFormat.DefaultRequestHeaders.Add("Authorization","Bearer " + llmApiKeyFormat);


/*—————————————————————————————————————————————————————————*/


var clientEvolutionMessage = new HttpClient() {BaseAddress = new Uri(evolutionUrlMessage)};
var clientEvolutionBase64 = new HttpClient() {BaseAddress = new Uri(evolutionUrlBase64)};
clientEvolutionMessage.DefaultRequestHeaders.Add("apikey", evolutionApiKey);
clientEvolutionBase64.DefaultRequestHeaders.Add("apikey", evolutionApiKey);


/*—————————————————————————————————————————————————————————*/

var clientSAP = new HttpClient() {BaseAddress = new Uri(sapUrlServer + "?token=" + tokenQuery)};

/*—————————————————————————————————————————————————————————*/

var clientTranscription = new HttpClient() { BaseAddress = new Uri(transcriptionApiUrl) };
clientTranscription.DefaultRequestHeaders.Add("Authorization", "Bearer " + transcriptionApiKey);





/*—————————————————————————————————————————————————————————*/
/********* Fonctions locales pour factoriser le code *******/
/*—————————————————————————————————————————————————————————*/

/* [AcceptHttpResponseToJson] prend un [HttpResponseMessage] et renvoi le json prêt à en tirer les données */
async Task<JsonDocument> AcceptHttpResponseToJson(HttpResponseMessage response)
{
    JsonDocument json;
    try
    {
        var content = await response.Content.ReadAsStringAsync();
        //Console.WriteLine("[log] ——— Accepted response : ———\n \n" + content + "\n \n");
        json = JsonDocument.Parse(content);
    }
    catch (Exception e)
    {
        Console.WriteLine("Exception caught in accepting HTTP Request with " + response.ToString() + " :\n");
        throw;
    }
    return json;
}


/* [SendBodyToClient] envoi l'object [a] par une requête POST par le client [client] */
async Task<HttpResponseMessage> SendBodyToClient(object a, HttpClient client)
{
    HttpResponseMessage response;
    try
    {
        var requestJson = JsonSerializer.Serialize(a);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        response = await client.PostAsync("", content);
        
        //response.EnsureSuccessStatusCode();
        
    }
    catch (Exception e)
    {
        Console.WriteLine("\nException caught in sending HTTP Request with " + a.ToString() + " :\n");
        Console.WriteLine(e.Message);
        throw;
    }
    
    return response;
}



/*—————————————————————————————————————————————————————————*/

/* [SaveMessageToConversation] enregistre le message [content] dans la DB conversations avec le role [role] et numéro [number] */
void SaveMessageToConversation(string role, string msg, string number)
{
    using (Aes aes = Aes.Create())
    {
        string encryptedContent = EncryptStringToBytes_Aes(msg, aes_key, aes.IV);
        string content = Convert.ToBase64String(aes.IV) + ":" + encryptedContent;
        
        using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringConversation))
        {
            string query = "INSERT INTO conversation (role, encryptedcontent, number) VALUES (@role, @msg, @num);";
            NpgsqlCommand command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@role", role);
            command.Parameters.AddWithValue("@msg",  content);
            command.Parameters.AddWithValue("@num",  number);   /*On met le même numéro de l'utilisateur pour bien reprendre la conversation entière après*/
            connection.Open();
            command.ExecuteNonQuery();
            
        }
    }
}


/* [GetLastMessagesOfConversation] retourne les [n] derniers messages de la conversation de l'utilisateur avec le numéro [numUser] avec le LLM. */
List<object> GetLastMessagesOfConversation(string numUser, int n)
{
    var conversation = new List<object>();
    
    
    using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringConversation))
    {
        string query = "SELECT role, encryptedcontent FROM conversation WHERE number = @num ORDER BY date DESC LIMIT @n";
        NpgsqlCommand command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@num", numUser);
        command.Parameters.AddWithValue("@n",n);
        connection.Open();
        NpgsqlDataReader readerConversation = command.ExecuteReader();
    
        try
        {
            while (readerConversation.Read())
            {
                var previousEncryptedContent = readerConversation.GetFieldValue<string>(1);

                /*role*/
                var previousRole = readerConversation.GetFieldValue<string>(0);
                
                /*IV*/
                var previousIVString = previousEncryptedContent.Split(":")[0];
                var previousIV = Convert.FromBase64String(previousIVString);
                
                /*msg*/
                var previousMsg = previousEncryptedContent.Split(":")[1];
                var previousContent = DecryptStringFromBytes_Aes(previousMsg, aes_key, previousIV);
                
                conversation.Add(new {role = previousRole, content = previousContent});
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception caught in getting last messages of number " + numUser + " :\n");
            Console.WriteLine(e);
            throw;
        }
        finally{ readerConversation.Close(); }
        
        connection.Close();
    
    }
    

    return conversation;
}


/* [SaveProcessedMessage] enregistre le message avec l'id [id] dans la db pour éviter une loop sur evolution api */
void SaveProcessedMessage(string id)
{
    using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringConversation))
    {
        string query = "INSERT INTO processed_messages (message_id) VALUES (@id)";
        NpgsqlCommand command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@id", id);
        connection.Open();
        command.ExecuteNonQuery();
    }
}


/* [IsMessageProcessed] vérifie si le message avec l'id [id] a été traité ou non. */
bool IsMessageProcessed(string id)
{
    bool used = false;
    
    using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringConversation))
    {
        string query = "SELECT * FROM processed_messages WHERE message_id = @id";
        NpgsqlCommand command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@id", id);
        connection.Open();
        NpgsqlDataReader reader = command.ExecuteReader();

        try
        {
            if (reader.Read())
            {
                used = true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
    }

    return used;
}


/*—————————————————————————————————————————————————————————*/
/* [ExtractAudioMoessage] extrait le message text de l'audio présent dans le msg JSON envoyé par Evolution API avec une transcription externe */
async Task<string> ExtractAudioMessage(JsonDocument json)
{
    //Console.WriteLine(json.RootElement.ToString());
    string id = json.RootElement.GetProperty("data").GetProperty("key").GetProperty("id").ToString();

    var messageBody = new
    {
        message = new
        {
            key = new { id = id }
        },
        convertToMp4 = false
    };
    
    //Console.WriteLine("——————Extract 1———————");
    var reponse = await SendBodyToClient(messageBody, clientEvolutionBase64);
    var base64Json = await AcceptHttpResponseToJson(reponse);
    //Console.WriteLine(base64Json.RootElement.ToString());
    var base64 = base64Json.RootElement.GetProperty("base64").ToString();
    
    //Console.WriteLine("——————Extract 2———————");

    byte[] converted = Convert.FromBase64String(base64);
    
    MultipartFormDataContent content = new MultipartFormDataContent();
    
    var fileStream = new MemoryStream(converted);
    var fileContent = new StreamContent(fileStream);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");
    
    content.Add(new StringContent(transcriptionModel),"model");
    content.Add(fileContent, "file","audio.ogg");
    
    var response = await clientTranscription.PostAsync("", content);
    JsonDocument jsonResponse = await AcceptHttpResponseToJson(response);
    
    
    return jsonResponse.RootElement.GetProperty("text").ToString(); 
}






/*—————————————————————————————————————————————————————————*/
/*—————————————————————————————————————————————————————————*/

app.MapPost("/api/webhook", async (HttpContext ctx) =>
{
    Console.WriteLine("begin\n");
    /*———————————————————
     *
     * Etape 0 : Vérification du token
     *
     * —————————————————*/
    var tokenVerification = app.Configuration["TOKEN_WEBHOOK"];
    var tokenRequest = ctx.Request.Query["token"];

    if (tokenRequest != tokenVerification)
    {
        Console.WriteLine("refused");
        return Results.StatusCode(401);
    }
    
    /*———————————————————
     *
     * Etape 1 : extraction du message et numéro de l'utilisateur 
     *
     * ———————————————————
     * */
    
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var json = JsonDocument.Parse(body);

    string numUser ;
    string msgType ;
    string id      ;
    
    try
    { 
        numUser = json.RootElement.GetProperty("data").GetProperty("key").GetProperty("remoteJid").ToString().Split("@")[0];
        msgType = json.RootElement.GetProperty("data").GetProperty("messageType").ToString();
        id      = json.RootElement.GetProperty("data").GetProperty("key").GetProperty("id").ToString();
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        return Results.BadRequest();
    }
    
    /* Vérification si le message a déjà été traité pour éviter les re-traitements  */
    if (IsMessageProcessed(id))
    {
        Console.WriteLine("already processed");
        return Results.Ok();
    }
    
    
    string msgUser;
    
    
    /* Extraction du message selon son type */
    if (msgType == "conversation")
    {
        msgUser = json.RootElement.GetProperty("data").GetProperty("message").GetProperty("conversation").ToString();
    }
    else if (msgType == "audioMessage")
    {
        msgUser = await ExtractAudioMessage(json);
    }
    else
    {
        /* Envoyer message spécial quand le msg n'est pas un audio ou texte */
        var respondBody = new
        {
            number = numUser,
            text = "L'assistant ne peut comprendre que des messages textes ou vocaux."
        };
        var respondBot = await SendBodyToClient(respondBody, clientEvolutionMessage);
        
        SaveProcessedMessage(id);
        
        Console.WriteLine("finish1");
        return Results.Ok();
    }
    
    
    /* Enrengistrement du message user dans la DB conversations */
    SaveMessageToConversation("user",msgUser,numUser);




    
    /*—————————————————————————
     *
     * Etape 2 : Discussion avec DeepSeek
     *
     * ————————————————————————
     */
    
    /* Récupération des 25 derniers messages pour donner du context au LLM */
    var conversation = GetLastMessagesOfConversation(numUser, 25);
   
    conversation.Reverse();  /* les msg doivent être pris selon les plus récents */


    var messagesRequest = new List<object>();
    
    messagesRequest.Add(new { role = "system", content = promptSystemSapQuery});
    messagesRequest.AddRange(conversation);
    messagesRequest.Add(new { role = "user", content = msgUser});
    
    var requestBody = new
    {
        model = modelLlmDiscussion,
        messages = messagesRequest,
        temperature = 0.3,
        response_format = new {type = "json_object" },
        thinking = new {type = "disabled"}      //Pour deepseek. Certains API LLM refuse ceci
    };

    var response = await SendBodyToClient(requestBody, clientLlmDiscussion);
    
    
    
    
    /*—————————————————————————
     *
     * Etape 3 : Envoi de la requête SQL vers le SAPQueryServer
     *
     * ————————————————————————
     */
    var result = await response.Content.ReadAsStringAsync();
    //Console.WriteLine("——————Parse3-1———————");
    var jsonResult = JsonDocument.Parse(result);
    //Console.WriteLine("——————Parse3-2———————");
    string botContent;
    try
    {
        botContent = jsonResult.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").ToString();
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message + "\nReturned response by API LLM Query :\n" + jsonResult.RootElement.ToString());
        botContent = "Message automatique : Le bot n'est pas disponible pour le moment.";
    }
    

    //botContent = botContent.Split('\n')[0].Trim();
    
    /* On prend la query dans la réponse json du LLM */
    //Console.WriteLine("——————Parse4-1———————");
    Console.WriteLine(botContent.ToString());
    

    
    if (botContent.Trim().StartsWith("```"))
    {
        var tmp = botContent.Trim().Split('\n');
        botContent = String.Join('\n', tmp.Skip(1).Take(tmp.Length - 2)).Trim();
    } 
    else if (!botContent.StartsWith("{"))
    {
        
        /*Le bot répond parfois avec des messages vides*/
        if (botContent.Trim() != "")
        {
            await SendBodyToClient(new { number = numUser, text = botContent }, clientEvolutionMessage);
            
            SaveMessageToConversation("assistant",botContent,numUser);
           
        }
        
        /* Enregistrement du message LLM dans les conversations */
        SaveProcessedMessage(id);
        
        Console.WriteLine("finish2 : " + botContent);
        return Results.Ok();
    } 
    
    
    
    var sqlJson = JsonDocument.Parse(botContent);
    //Console.WriteLine("——————Parse4-2———————");
    var SQLquery = sqlJson.RootElement.GetProperty("query").ToString();

    
    /* Si le msg user n'était pas une consultation de données, répondre simplement par la réponse du LLM */
    if (SQLquery == "")
    {
        var responseWhatsApp = sqlJson.RootElement.GetProperty("response").ToString();
        
        
        var respondBodySimple = new
        {
            number = numUser,
            text = responseWhatsApp  
        };
        
        var respondBotSimple = await SendBodyToClient(respondBodySimple, clientEvolutionMessage);
        
        /* Enregistrement du message LLM dans les conversations */
        SaveMessageToConversation("assistant",responseWhatsApp,numUser);
        SaveProcessedMessage(id);
        
        Console.WriteLine("finish3");
        return Results.Ok();
        
    }
    
    //Console.WriteLine("\n------Requête SQL envoyée : -------\n \n"+SQLquery+"\n \n----------------------\n");

    HttpResponseMessage dataHttp = null;
    Exception? exc = null;
    try
    {
        dataHttp = await SendBodyToClient(new { query = SQLquery, number = numUser }, clientSAP);
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        exc = e;
    }
    
    
    
    
    
    
    /*————————————————————————
     *
     * Etape 4 : Formattage de la réponse par le LLM
     *
     * ————————————————————————
     */
    string data = "";
    
    /* Si le numéro n'est pas trouvé, on prévient le LLM de dire au client de contacter le support pour ajouter son numéro avec sa db
     * Si la requête n'est pas une lecture (SELECT), on prévient cela. */
    if (exc != null || dataHttp == null)
    {
        data = "SAPQuery server not available : " + exc.Message;
    }
    else if (dataHttp.StatusCode == HttpStatusCode.OK)
    {
        data = await dataHttp.Content.ReadAsStringAsync();
    }
    else if (dataHttp.StatusCode == HttpStatusCode.NotFound)
    {
        data = "Your phone number isn't saved in our client's database number. Please contact the support.";
    }
    else if (dataHttp.StatusCode == HttpStatusCode.Forbidden)
    {
        data = "Not authorized. You can only read data with SELECT statement.";
    }
    else
    {
        data = "SAPQuery server not available " ;
    }
    
    //Console.WriteLine("[log] - SAP data : \n" + data + " \n" );
    
    
    var messagesFormat = new List<object>();
    
    messagesFormat.Add(new { role = "system", content = promptSystemFormat});
    messagesFormat.AddRange(conversation);
    messagesFormat.Add(new { role = "user", content = "Question utilisateur : " + msgUser + "\nDonnées à formatter retournée par le serveur : " + data});
    
    
    
    var formatRequestBody = new
    {
        model = modelLlmFormat,
        messages = messagesFormat,
        temperature = 0.5,
        thinking = new {type = "disabled"}      //Pour deepseek. Certains API LLM refuse ceci
    };

    var formatResponse = await SendBodyToClient(formatRequestBody, clientLlmFormat);
    
    
    
    
    /*————————————————————————
     *
     * Etape 5 : Réponse du bot vers whatsapp
     *
     * ————————————————————————
     */
    var jsonFinal = await AcceptHttpResponseToJson(formatResponse);
    string botResponse;
    try
    {
        botResponse = jsonFinal.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").ToString();
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message + "\nReturned response by API LLM Format : \n" + jsonFinal.RootElement.ToString());
        botResponse = "Le bot de formattage n'est pas disponible. Voici les informations tirées : \n" + data;
    }

    var finalRespondBody = new
    {
      number = numUser,
      text = botResponse  
    };
    var finalRespondBot = await SendBodyToClient(finalRespondBody, clientEvolutionMessage);
    
    /* Enregistrement du message LLM dans la DB conversations */
    SaveMessageToConversation("assistant",botResponse,numUser);
    
    var respondResult = await finalRespondBot.Content.ReadAsStringAsync();
    SaveProcessedMessage(id);
    
    Console.WriteLine("finish4");
    return Results.Ok();
});



app.Run();
