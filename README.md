# Chatbot IA WhatsApp pour les requêtes de données ERP

Un chatbot WhatsApp multi-tenant permettant aux utilisateurs métier d'interroger leur base de données ERP en langage naturel — sans connaissance en SQL et sans avoir à naviguer dans une interface ERP complexe.

Projet réalisé dans le cadre d'un stage de 2 mois en ingénierie logicielle.

## Vue d'ensemble

Les logiciels ERP utilisés en entreprise rendent souvent des questions simples du quotidien (« Quel est mon chiffre d'affaires ce mois-ci ? », « Quelles factures sont impayées ? ») plus difficiles à répondre qu'elles ne devraient l'être, obligeant les utilisateurs à naviguer à travers plusieurs menus. Ce projet résout ce problème en permettant aux utilisateurs d'envoyer simplement un message à un numéro WhatsApp et de recevoir instantanément une réponse claire en langage naturel.

Le système prend en charge plusieurs clients indépendants utilisant un même numéro WhatsApp, chacun disposant d'une connexion isolée à sa propre base de données. Il prend également en charge les messages texte et vocaux.

## Fonctionnement

1. Un utilisateur envoie une question au chatbot sur WhatsApp (texte ou message vocal).
2. Le message est transmis à un backend .NET via une intégration une API WhatsApp.
3. Un LLM traduit la question formulée en langage naturel en une requête SQL.
4. La requête est validée puis exécutée sur la base de données du client via un service dédié aux requêtes.
5. Les résultats sont envoyés au LLM, qui les transforme en une réponse claire et compréhensible en langage naturel.
6. La réponse est envoyée à l'utilisateur sur WhatsApp.

## Architecture

Le système est composé de trois services .NET indépendants, orchestrés avec Docker Compose :

- **ChatBot** — reçoit les messages WhatsApp entrants, gère l'historique des conversations, orchestre les deux appels au LLM (génération SQL et formatage de la réponse) et gère la transcription des messages vocaux.
- **SAPQueryServer** — reçoit les requêtes SQL validées et les exécute sur la base de données du client concerné, en utilisant un compte de base de données restreint et spécifique à chaque client.
- **AddClient** — outil en ligne de commande utilisé pour ajouter un nouveau client : il crée un compte de base de données dédié avec des droits limités en lecture seule et stocke de manière sécurisée sa chaîne de connexion chiffrée.

Une base de données PostgreSQL locale stocke l'historique des conversations ainsi que les informations de routage des clients (c'est-à-dire quel numéro de téléphone est associé à quelle base de données).

```scss
Utilisateur WhatsApp
     │
     ▼
API WhatsApp (webhook)
     │
     ▼
Service ChatBot ──► LLM (texte vers SQL) ──► SAPQueryServer ──► Base de données client
     │                                                                    │
     └────────────────────◄── LLM (formatage) ◄────────────────--─────────┘
     │
     ▼
Utilisateur WhatsApp (réponse)
```

## Fonctionnalités principales

- **Langage naturel vers SQL** — les utilisateurs n'ont pas besoin de connaître le SQL ni le schéma de la base de données.
- **Prise en charge des messages vocaux** — les messages audio sont automatiquement transcrits avant d'être traités comme une requête textuelle.
- **Routage multi-tenant** — un seul numéro WhatsApp peut servir plusieurs clients indépendants, chacun étant associé à sa propre base de données isolée.
- **Mémoire conversationnelle** — l'historique récent des messages est transmis au LLM afin que les questions de suivi (« Et pour le mois dernier ? ») soient comprises dans leur contexte.

## Sécurité

La sécurité était un aspect central du projet compte tenu de la sensibilité des données manipulées (identifiants de bases de données, données métier et contenu des conversations) :

- **Chiffrement au repos** — les chaînes de connexion aux bases de données et l'historique des conversations sont chiffrés avec AES-256 avant leur stockage.
- **Principe du moindre privilège** — chaque client dispose d'un compte de base de données dédié, généré automatiquement et limité aux droits de lecture seule (`SELECT`). Même si une chaîne de connexion était compromise, elle ne pourrait pas être utilisée pour modifier ou supprimer des données.
- **Validation indépendante des requêtes** — chaque requête SQL générée par le LLM est vérifiée à l'aide d'une liste de mots-clés interdits liés à la modification ou à la suppression de données avant son exécution, quel que soit le modèle utilisé. Cette mesure a été ajoutée après avoir constaté qu'un précédent choix de LLM ne respectait pas systématiquement l'instruction « lecture seule » définie dans son prompt système.
- **Authentification inter-services** — les requêtes entre les services internes sont authentifiées à l'aide d'un jeton partagé, empêchant des services non autorisés d'injecter des requêtes ou de faux événements webhook.
- **Gestion des secrets** — toutes les clés API et informations d'identification sont fournies via des variables d'environnement et ne sont jamais enregistrées dans le dépôt.

## Stack technique

| Couche | Technologie |
|---|---|
| Backend | C# / .NET (ASP.NET Core Minimal APIs) |
| Bases de données | PostgreSQL (données applicatives), SQL Server (données ERP des clients) |
| IA | API LLM pour la génération SQL et le formatage des réponses, Whisper pour la transcription vocale |
| Messagerie | Intégration avec l'API Evolution pour WhatsApp|
| Infrastructure | Docker, Docker Compose |
| Déploiement | PaaS auto-hébergé (Coolify) |

## Ce que j'ai appris

Ce projet a été ma première expérience d'intégration d'un LLM dans un système de production allant au-delà du simple prompting. J'ai notamment découvert les compromis liés à la fiabilité des modèles et pourquoi la sortie d'un LLM ne doit jamais être considérée comme l'unique couche de sécurité autour d'une opération sensible telle que l'exécution de requêtes SQL.

Ce projet m'a également permis d'acquérir une expérience pratique en architecture multi-services, en conception sécurisée de bases de données, en conteneurisation et en déploiement d'applications multi-conteneurs dans un environnement de production.
