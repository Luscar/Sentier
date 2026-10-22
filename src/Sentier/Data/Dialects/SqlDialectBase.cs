using System.Data;
using Sentier.Configuration;

namespace Sentier.Data.Dialects;

/// <summary>DDL commun : seuls les types de colonnes et les séquences diffèrent entre bases.</summary>
public abstract class SqlDialectBase : ISqlDialect
{
    protected SqlDialectBase(SentierNames names)
    {
        Names = names ?? throw new ArgumentNullException(nameof(names));
    }

    protected SentierNames Names { get; }

    public abstract SentierDatabase Database { get; }

    public abstract string ForUpdateClause { get; }

    protected abstract string BigIntType { get; }
    protected abstract string IntType { get; }
    protected abstract string ClobType { get; }
    protected abstract string TimestampType { get; }
    protected abstract string StringType(int length);

    public abstract Task<long> NextValueAsync(IDbConnection connection, IDbTransaction? transaction, string sequenceName, CancellationToken cancellationToken);

    public abstract string Limit(string selectSql, int count);

    public virtual IReadOnlyList<string> CreateSchemaScript()
    {
        var n = Names;
        string Big = BigIntType, Int = IntType, Clob = ClobType, Ts = TimestampType;
        string Str(int len) => StringType(len);

        var statements = new List<string>();
        statements.AddRange(CreateSequences());

        statements.Add($@"CREATE TABLE {n.ProcessDefinition} (
    ID            {Big} NOT NULL,
    DEF_KEY       {Str(100)} NOT NULL,
    VERSION_NO    {Int} NOT NULL,
    NAME          {Str(200)},
    CONTENT       {Clob} NOT NULL,
    CONTENT_HASH  {Str(64)} NOT NULL,
    CREATED_AT    {Ts} NOT NULL,
    CONSTRAINT {n.Name("PK_PROC_DEF")} PRIMARY KEY (ID),
    CONSTRAINT {n.Name("UK_PROC_DEF")} UNIQUE (DEF_KEY, VERSION_NO)
)");

        statements.Add($@"CREATE TABLE {n.ProcessInstance} (
    ID                  {Big} NOT NULL,
    DEF_ID              {Big} NOT NULL,
    DEF_KEY             {Str(100)} NOT NULL,
    DEF_VERSION         {Int} NOT NULL,
    BUSINESS_KEY        {Str(200)},
    STATUS              {Str(20)} NOT NULL,
    CURRENT_NODE        {Str(100)},
    VARIABLES           {Clob},
    PARENT_ID           {Big},
    PARENT_NODE_INST_ID {Big},
    ROOT_ID             {Big} NOT NULL,
    ROW_VERSION         {Int} NOT NULL,
    CREATED_AT          {Ts} NOT NULL,
    UPDATED_AT          {Ts} NOT NULL,
    ENDED_AT            {Ts},
    CONSTRAINT {n.Name("PK_PROC_INST")} PRIMARY KEY (ID),
    CONSTRAINT {n.Name("FK_PROC_INST_DEF")} FOREIGN KEY (DEF_ID) REFERENCES {n.ProcessDefinition} (ID),
    CONSTRAINT {n.Name("FK_PROC_INST_PAR")} FOREIGN KEY (PARENT_ID) REFERENCES {n.ProcessInstance} (ID)
)");

        statements.Add($@"CREATE TABLE {n.NodeInstance} (
    ID            {Big} NOT NULL,
    PROC_INST_ID  {Big} NOT NULL,
    NODE_ID       {Str(100)} NOT NULL,
    NODE_TYPE     {Str(20)} NOT NULL,
    STATUS        {Str(20)} NOT NULL,
    TRANSITION_ID {Str(210)},
    STARTED_AT    {Ts} NOT NULL,
    ENDED_AT      {Ts},
    CONSTRAINT {n.Name("PK_NODE_INST")} PRIMARY KEY (ID),
    CONSTRAINT {n.Name("FK_NODE_INST_PI")} FOREIGN KEY (PROC_INST_ID) REFERENCES {n.ProcessInstance} (ID)
)");

        statements.Add($@"CREATE TABLE {n.Timer} (
    ID            {Big} NOT NULL,
    PROC_INST_ID  {Big} NOT NULL,
    NODE_INST_ID  {Big} NOT NULL,
    DUE_AT        {Ts} NOT NULL,
    STATUS        {Str(20)} NOT NULL,
    CREATED_AT    {Ts} NOT NULL,
    FIRED_AT      {Ts},
    CONSTRAINT {n.Name("PK_TIMER")} PRIMARY KEY (ID),
    CONSTRAINT {n.Name("FK_TIMER_PI")} FOREIGN KEY (PROC_INST_ID) REFERENCES {n.ProcessInstance} (ID),
    CONSTRAINT {n.Name("FK_TIMER_NI")} FOREIGN KEY (NODE_INST_ID) REFERENCES {n.NodeInstance} (ID)
)");

        statements.Add($@"CREATE TABLE {n.SignalSubscription} (
    ID              {Big} NOT NULL,
    PROC_INST_ID    {Big} NOT NULL,
    NODE_INST_ID    {Big} NOT NULL,
    SIGNAL_NAME     {Str(100)} NOT NULL,
    CORRELATION_KEY {Str(200)},
    STATUS          {Str(20)} NOT NULL,
    CREATED_AT      {Ts} NOT NULL,
    RECEIVED_AT     {Ts},
    CONSTRAINT {n.Name("PK_SIGNAL_SUB")} PRIMARY KEY (ID),
    CONSTRAINT {n.Name("FK_SIGNAL_SUB_PI")} FOREIGN KEY (PROC_INST_ID) REFERENCES {n.ProcessInstance} (ID),
    CONSTRAINT {n.Name("FK_SIGNAL_SUB_NI")} FOREIGN KEY (NODE_INST_ID) REFERENCES {n.NodeInstance} (ID)
)");

        statements.Add($@"CREATE TABLE {n.UserTask} (
    ID            {Big} NOT NULL,
    PROC_INST_ID  {Big} NOT NULL,
    NODE_INST_ID  {Big} NOT NULL,
    TASK_CODE     {Str(100)} NOT NULL,
    EXTERNAL_ID   {Str(100)},
    STATUS        {Str(20)} NOT NULL,
    CREATED_AT    {Ts} NOT NULL,
    COMPLETED_AT  {Ts},
    COMPLETED_BY  {Str(100)},
    OUTCOME       {Str(100)},
    CONSTRAINT {n.Name("PK_USER_TASK")} PRIMARY KEY (ID),
    CONSTRAINT {n.Name("FK_USER_TASK_PI")} FOREIGN KEY (PROC_INST_ID) REFERENCES {n.ProcessInstance} (ID),
    CONSTRAINT {n.Name("FK_USER_TASK_NI")} FOREIGN KEY (NODE_INST_ID) REFERENCES {n.NodeInstance} (ID)
)");

        statements.Add($"CREATE INDEX {n.Name("IX_PROC_INST_BK")} ON {n.ProcessInstance} (BUSINESS_KEY)");
        statements.Add($"CREATE INDEX {n.Name("IX_PROC_INST_PAR")} ON {n.ProcessInstance} (PARENT_ID)");
        statements.Add($"CREATE INDEX {n.Name("IX_PROC_INST_DEF")} ON {n.ProcessInstance} (DEF_ID)");
        statements.Add($"CREATE INDEX {n.Name("IX_NODE_INST_PI")} ON {n.NodeInstance} (PROC_INST_ID, STATUS)");
        statements.Add($"CREATE INDEX {n.Name("IX_TIMER_DUE")} ON {n.Timer} (STATUS, DUE_AT)");
        statements.Add($"CREATE INDEX {n.Name("IX_TIMER_PI")} ON {n.Timer} (PROC_INST_ID)");
        statements.Add($"CREATE INDEX {n.Name("IX_SIGNAL_SUB_NAME")} ON {n.SignalSubscription} (SIGNAL_NAME, STATUS, CORRELATION_KEY)");
        statements.Add($"CREATE INDEX {n.Name("IX_SIGNAL_SUB_PI")} ON {n.SignalSubscription} (PROC_INST_ID)");
        statements.Add($"CREATE INDEX {n.Name("IX_USER_TASK_EXT")} ON {n.UserTask} (EXTERNAL_ID)");
        statements.Add($"CREATE INDEX {n.Name("IX_USER_TASK_PI")} ON {n.UserTask} (PROC_INST_ID)");

        statements.Add($@"CREATE VIEW {n.WaitingInstancesView} AS
SELECT pi.ID           AS PROC_INST_ID,
       pi.DEF_KEY      AS DEF_KEY,
       pi.DEF_VERSION  AS DEF_VERSION,
       pi.BUSINESS_KEY AS BUSINESS_KEY,
       pi.ROOT_ID      AS ROOT_ID,
       ni.ID           AS NODE_INST_ID,
       ni.NODE_ID      AS NODE_ID,
       ni.NODE_TYPE    AS NODE_TYPE,
       ni.STARTED_AT   AS WAITING_SINCE
FROM {n.ProcessInstance} pi
JOIN {n.NodeInstance} ni ON ni.PROC_INST_ID = pi.ID AND ni.STATUS = 'Waiting'
WHERE pi.STATUS = 'Waiting'");

        statements.Add($@"CREATE VIEW {n.OpenUserTasksView} AS
SELECT ut.ID           AS USER_TASK_ID,
       ut.TASK_CODE    AS TASK_CODE,
       ut.EXTERNAL_ID  AS EXTERNAL_ID,
       ut.CREATED_AT   AS CREATED_AT,
       ut.NODE_INST_ID AS NODE_INST_ID,
       pi.ID           AS PROC_INST_ID,
       pi.DEF_KEY      AS DEF_KEY,
       pi.BUSINESS_KEY AS BUSINESS_KEY
FROM {n.UserTask} ut
JOIN {n.ProcessInstance} pi ON pi.ID = ut.PROC_INST_ID
WHERE ut.STATUS = 'Pending'");

        return statements;
    }

    public virtual IReadOnlyList<string> DropSchemaScript()
    {
        var n = Names;
        var statements = new List<string>
        {
            DropView(n.OpenUserTasksView),
            DropView(n.WaitingInstancesView),
            DropTable(n.UserTask),
            DropTable(n.SignalSubscription),
            DropTable(n.Timer),
            DropTable(n.NodeInstance),
            DropTable(n.ProcessInstance),
            DropTable(n.ProcessDefinition),
        };
        statements.AddRange(DropSequences());
        return statements;
    }

    protected abstract IEnumerable<string> CreateSequences();
    protected abstract IEnumerable<string> DropSequences();
    protected abstract string DropTable(string table);
    protected abstract string DropView(string view);
}
