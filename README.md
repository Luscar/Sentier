# Sentier

Traitements de parcours fonctionnel à haut volume.

Sentier est un moteur BPM embarqué, distribué sous forme de package NuGet pour .NET 8.

## Principes

- **Exécution séquentielle.** Un appel au moteur (démarrage, complétion de tâche, signal, minuterie) exécute
  les noeuds l'un après l'autre jusqu'à un noeud qui suspend l'instance (attente temporelle, attente de signal,
  tâche interactive, sous-processus suspendu) ou jusqu'à un noeud de fin.
- **La transaction appartient au client.** Le client injecte sa connexion `IDbConnection` et sa transaction
  via `ISentierDbSession`. Sentier n'ouvre, ne valide et n'annule jamais rien : si un noeud lève une exception,
  le client annule sa transaction et l'instance revient à son dernier point d'arrêt.
- **Bassin de tâches existant.** Les tâches interactives passent par l'interface `ITaskPool`, implémentée
  par le client.
- **Dapper, Oracle et SQLite.** Les clés techniques sont des `long` issues de séquences Oracle (émulées par
  une table sous SQLite). Toutes les tables, vues, séquences, index et contraintes sont préfixés :
  plusieurs systèmes peuvent donc partager le même schéma.

## Types de noeuds

| Noeud | Classe | Comportement |
|---|---|---|
| Départ | `StartNode` | Point d'entrée unique. |
| Systémique | `SystemNode` | Exécute un `ISystemTaskHandler` puis continue. Le handler peut choisir sa transition. |
| Attente temporelle | `TimerWaitNode` | Suspend jusqu'à une durée ou une date d'échéance (fixe ou lue dans une variable). |
| Attente de signal | `SignalWaitNode` | Suspend jusqu'à `SendSignalAsync`, avec une clé de corrélation facultative. |
| Interactif | `UserTaskNode` | Crée une tâche dans le bassin du client et suspend jusqu'à sa complétion. |
| Décision | `DecisionNode` | Choisit la transition via un `IDecisionHandler` ou la valeur d'une variable. |
| Sous-processus | `SubProcessNode` | Démarre un processus enfant et attend sa fin, avec mappage des variables en entrée et en sortie. |
| Fin | `EndNode` | Termine l'instance et reprend le parent s'il y en a un. |

## Schéma (préfixe `AT`)

| Objet | Rôle |
|---|---|
| `AT_PROC_DEF` | Définitions versionnées (JSON + empreinte) |
| `AT_PROC_INST` | Instances : statut, noeud courant, variables JSON, parent/racine, `ROW_VERSION` (verrou optimiste) |
| `AT_NODE_INST` | Historique des noeuds exécutés |
| `AT_TIMER` | Minuteries en attente |
| `AT_SIGNAL_SUB` | Attentes de signal |
| `AT_USER_TASK` | Tâches interactives et lien vers le bassin (`EXTERNAL_ID`) |
| `AT_V_INST_ATTENTE`, `AT_V_TACHE_OUVERTE` | Vues de suivi |
| `AT_SEQ_*` | Séquences (une par table) |

Pour générer le script à fournir au DBA :

```csharp
string ddl = SentierSchema.GetCreateScript(SentierDatabase.Oracle, "AT");
```

Pour créer le schéma directement (par exemple sur SQLite en test) :

```csharp
await SentierSchema.CreateAsync(connection, SentierDatabase.Sqlite, "AT");
```

## Configuration

```csharp
services.AddSentier(o =>
    {
        o.TablePrefix = "AT";
        o.Database = SentierDatabase.Oracle;
    })
    .UseConnection(sp => sp.GetRequiredService<MonUnitOfWork>().Connection,
                   sp => sp.GetRequiredService<MonUnitOfWork>().Transaction)
    .AddTaskPool<MonBassinDeTaches>()
    .AddSystemTask<ValiderDemandeHandler>("ValiderDemande")
    .AddDecision("Montant", (ctx, ct) =>
        Task.FromResult<string?>(ctx.Variables.Get<decimal>("montant") > 1000 ? "eleve" : "normal"));
```

## Définition et exécution

```csharp
var definition = ProcessDefinitionBuilder.Create("DEMANDE", "Traitement d'une demande")
    .Start()
    .SystemTask("valider", "ValiderDemande")
    .Decision("montant", handler: "Montant")
    .UserTask("approuver", t => { t.Title = "Approuver la demande"; t.CandidateGroups.Add("GESTIONNAIRES"); })
    .WaitSignal("paiement", "PaiementRecu", correlateOnBusinessKey: true)
    .WaitDuration("delai", TimeSpan.FromDays(2))
    .End()
    .Chain("start", "valider", "montant")
    .Transition("montant", "approuver", name: "eleve")
    .Transition("montant", "paiement", name: "normal", isDefault: true)
    .Chain("approuver", "paiement", "delai", "end")
    .Build();

await definitions.DeployAsync(definition);                       // nouvelle version seulement si le contenu change
var id = await engine.StartProcessAsync("DEMANDE", "DOS-42", new Dictionary<string, object?> { ["montant"] = 1500m });

// plus tard, chacun dans une transaction du client :
await engine.CompleteUserTaskByExternalIdAsync("T-981", new UserTaskCompletion { CompletedBy = "jdupont" });
await engine.SendSignalAsync("PaiementRecu", correlationKey: "DOS-42");
foreach (var timerId in await engine.GetDueTimersAsync()) { /* une transaction par minuterie */ await engine.FireTimerAsync(timerId); }
```

## Notes Oracle

- Oracle 12c ou plus récent (`FETCH FIRST`). Avec un préfixe de 10 caractères au maximum, les noms
  d'objets restent sous la limite de 30 caractères.
- Les reprises verrouillent la ligne de l'instance (`SELECT ... FOR UPDATE`), en plus du `ROW_VERSION`.
- Pour des variables JSON de plus de 4000 caractères, renseigner `SentierOptions.LargeTextParameter`
  avec un `ICustomQueryParameter` qui lie un paramètre `OracleDbType.Clob`.
