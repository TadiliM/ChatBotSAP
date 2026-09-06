CREATE DATABASE conversations;
\connect conversations;

CREATE TABLE IF NOT EXISTS conversation (
    id SERIAL PRIMARY KEY,
    role VARCHAR(100),
    number VARCHAR(15),
    date TIMESTAMP DEFAULT NOW(),
    encryptedcontent VARCHAR
);

CREATE TABLE IF NOT EXISTS processed_messages (
    message_id VARCHAR(255) PRIMARY KEY,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX ON processed_messages (message_id);

GRANT CONNECT ON DATABASE conversations TO evolutionrestricted;
GRANT USAGE ON SCHEMA public TO evolutionrestricted;
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA public TO evolutionrestricted;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO evolutionrestricted;

CREATE DATABASE sap_config;
\connect sap_config;
           
CREATE TABLE IF NOT EXISTS users_sap (
    id SERIAL PRIMARY KEY,
    number VARCHAR(15),
    db_connection VARCHAR
);


-- TODO : Ne pas oublier de changer le mot de passe
CREATE USER evolutionrestricted PASSWORD 'Azerqsdfwxcv123!';


GRANT CONNECT ON DATABASE sap_config TO evolutionrestricted;
GRANT USAGE ON SCHEMA public TO evolutionrestricted;
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA public TO evolutionrestricted;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO evolutionrestricted;


