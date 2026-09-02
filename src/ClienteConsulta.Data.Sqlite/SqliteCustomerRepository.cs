using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;
using Microsoft.Data.Sqlite;

namespace ClienteConsulta.Data.Sqlite;

/// <summary>
/// Banco de dados interno (SQLite, um único arquivo) que fica junto com o aplicativo.
/// É a fonte de dados padrão: nenhum PC precisa ter a planilha Excel para o app
/// funcionar — ela só é usada como origem quando alguém importa uma atualização
/// (ver <see cref="ReplaceAllAsync"/>).
/// </summary>
public sealed class SqliteCustomerRepository : ICustomerRepository
{
    private static readonly string[] Columns =
    [
        "Codigo", "RazaoSocial", "Nome", "Apelido", "NomeFantasia", "NaturezaJuridica", "Contador",
        "TipoEndereco", "InicioAtividades", "Endereco", "Numero", "Bairro", "Complemento",
        "Municipio", "UF", "CEP", "Pais",
        "DataInscricao", "ClienteDesde", "DataGenerica",
        "Cnae", "RamoAtividade", "ResponsavelLegal", "Telefone", "Email",
        "CnpjCpf", "InscricaoEstadual", "InscricaoMunicipal", "InscricaoJuntaComercial", "CapitalSocial"
    ];

    private readonly Func<string> _dbPathProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<Customer>? _cache;

    public SqliteCustomerRepository(Func<string> dbPathProvider)
    {
        _dbPathProvider = dbPathProvider;
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is { } cached)
            return cached;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache ??= await Task.Run(Load, cancellationToken).ConfigureAwait(false);
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache = await Task.Run(Load, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Apaga todos os clientes e grava a lista informada — usado ao importar uma planilha atualizada.</summary>
    public async Task ReplaceAllAsync(IReadOnlyList<Customer> customers, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() => WriteAll(customers), cancellationToken).ConfigureAwait(false);
            _cache = customers;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Insere um único cliente cadastrado manualmente (tela "Cadastrar nova empresa"), sem
    /// mexer nos demais registros. Lança <see cref="DataSourceException"/> se o Código já
    /// existir (chave primária) — o formulário mostra essa mensagem para o usuário.
    /// </summary>
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() => InsertOne(customer), cancellationToken).ConfigureAwait(false);
            _cache = new List<Customer>(_cache ?? []) { customer };
        }
        finally
        {
            _gate.Release();
        }
    }

    private void InsertOne(Customer customer)
    {
        var path = _dbPathProvider();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            using var connection = OpenConnection(path);
            EnsureSchema(connection);

            using var insert = CreateInsertCommand(connection, null);
            BindCustomer(insert, customer);
            insert.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT (chave primária duplicada)
        {
            throw new DataSourceException($"Já existe uma empresa cadastrada com o código \"{customer.Codigo}\".", ex);
        }
        catch (Exception ex) when (ex is not DataSourceException)
        {
            throw new DataSourceException($"Não foi possível salvar no banco de dados interno:\n{path}\n\n{ex.Message}", ex);
        }
    }

    private IReadOnlyList<Customer> Load()
    {
        var path = _dbPathProvider();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new DataSourceException(
                "Nenhuma base de clientes ainda.\n\n" +
                "Abra as Configurações (ícone na bandeja do Windows) e use " +
                "\"Importar clientes...\" para carregar o relatório da Domínio, " +
                "ou \"+ Cadastrar nova empresa\" para adicionar uma manualmente.");

        try
        {
            using var connection = OpenConnection(path);
            EnsureSchema(connection);

            var list = new List<Customer>();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {string.Join(", ", Columns)} FROM Customers ORDER BY RazaoSocial";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var i = 0;
                string Str() => reader.GetString(i++);
                string? StrOrNull() => reader.IsDBNull(i++) ? null : reader.GetString(i - 1);

                list.Add(new Customer
                {
                    Codigo = Str(),
                    RazaoSocial = Str(),
                    Nome = StrOrNull(),
                    Apelido = StrOrNull(),
                    NomeFantasia = StrOrNull(),
                    NaturezaJuridica = StrOrNull(),
                    Contador = StrOrNull(),
                    TipoEndereco = StrOrNull(),
                    InicioAtividades = StrOrNull(),
                    Endereco = StrOrNull(),
                    Numero = StrOrNull(),
                    Bairro = StrOrNull(),
                    Complemento = StrOrNull(),
                    Municipio = Str(),
                    UF = Str(),
                    CEP = StrOrNull(),
                    Pais = StrOrNull(),
                    DataInscricao = StrOrNull(),
                    ClienteDesde = StrOrNull(),
                    DataGenerica = StrOrNull(),
                    Cnae = StrOrNull(),
                    RamoAtividade = StrOrNull(),
                    ResponsavelLegal = StrOrNull(),
                    Telefone = StrOrNull(),
                    Email = StrOrNull(),
                    CnpjCpf = Str(),
                    InscricaoEstadual = StrOrNull(),
                    InscricaoMunicipal = StrOrNull(),
                    InscricaoJuntaComercial = StrOrNull(),
                    CapitalSocial = StrOrNull(),
                });
            }

            return list;
        }
        catch (DataSourceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataSourceException($"Não foi possível ler o banco de dados interno:\n{path}\n\n{ex.Message}", ex);
        }
    }

    private void WriteAll(IReadOnlyList<Customer> customers)
    {
        var path = _dbPathProvider();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            using var connection = OpenConnection(path);
            EnsureSchema(connection);

            using var transaction = connection.BeginTransaction();

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM Customers";
                delete.ExecuteNonQuery();
            }

            using (var insert = CreateInsertCommand(connection, transaction))
            {
                foreach (var customer in customers)
                {
                    BindCustomer(insert, customer);
                    insert.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
        catch (Exception ex) when (ex is not DataSourceException)
        {
            throw new DataSourceException($"Não foi possível salvar no banco de dados interno:\n{path}\n\n{ex.Message}", ex);
        }
    }

    private static SqliteCommand CreateInsertCommand(SqliteConnection connection, SqliteTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        var paramNames = Columns.Select(c => "$" + char.ToLowerInvariant(c[0]) + c[1..]).ToArray();
        command.CommandText =
            $"INSERT INTO Customers ({string.Join(", ", Columns)}) VALUES ({string.Join(", ", paramNames)})";

        foreach (var name in paramNames)
            command.Parameters.Add(name, SqliteType.Text);

        return command;
    }

    private static void BindCustomer(SqliteCommand insert, Customer customer)
    {
        object?[] values =
        [
            customer.Codigo, customer.RazaoSocial, customer.Nome, customer.Apelido, customer.NomeFantasia,
            customer.NaturezaJuridica, customer.Contador, customer.TipoEndereco, customer.InicioAtividades,
            customer.Endereco, customer.Numero, customer.Bairro, customer.Complemento, customer.Municipio,
            customer.UF, customer.CEP, customer.Pais, customer.DataInscricao, customer.ClienteDesde,
            customer.DataGenerica, customer.Cnae, customer.RamoAtividade, customer.ResponsavelLegal,
            customer.Telefone, customer.Email, customer.CnpjCpf, customer.InscricaoEstadual,
            customer.InscricaoMunicipal, customer.InscricaoJuntaComercial, customer.CapitalSocial
        ];

        for (var i = 0; i < values.Length; i++)
            ((SqliteParameter)insert.Parameters[i]).Value = values[i] ?? (object)DBNull.Value;
    }

    private static SqliteConnection OpenConnection(string path)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        DropTableIfSchemaMismatch(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Customers (
                Codigo TEXT NOT NULL PRIMARY KEY,
                RazaoSocial TEXT NOT NULL,
                Nome TEXT NULL,
                Apelido TEXT NULL,
                NomeFantasia TEXT NULL,
                NaturezaJuridica TEXT NULL,
                Contador TEXT NULL,
                TipoEndereco TEXT NULL,
                InicioAtividades TEXT NULL,
                Endereco TEXT NULL,
                Numero TEXT NULL,
                Bairro TEXT NULL,
                Complemento TEXT NULL,
                Municipio TEXT NOT NULL,
                UF TEXT NOT NULL,
                CEP TEXT NULL,
                Pais TEXT NULL,
                DataInscricao TEXT NULL,
                ClienteDesde TEXT NULL,
                DataGenerica TEXT NULL,
                Cnae TEXT NULL,
                RamoAtividade TEXT NULL,
                ResponsavelLegal TEXT NULL,
                Telefone TEXT NULL,
                Email TEXT NULL,
                CnpjCpf TEXT NOT NULL,
                InscricaoEstadual TEXT NULL,
                InscricaoMunicipal TEXT NULL,
                InscricaoJuntaComercial TEXT NULL,
                CapitalSocial TEXT NULL
            )
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// O schema mudou bastante entre v1 e v2 (colunas novas, chave primária diferente).
    /// Em vez de migrar dado por dado, se uma tabela Customers já existir com um
    /// conjunto de colunas diferente do esperado, ela é recriada do zero — o fluxo de
    /// dados aqui é sempre "importar planilha inteira", nunca edição incremental.
    /// </summary>
    private static void DropTableIfSchemaMismatch(SqliteConnection connection)
    {
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(Customers)";
            using var reader = pragma.ExecuteReader();
            while (reader.Read())
                actual.Add(reader.GetString(1));
        }

        if (actual.Count == 0)
            return; // tabela ainda não existe

        var expected = new HashSet<string>(Columns, StringComparer.OrdinalIgnoreCase);
        if (actual.SetEquals(expected))
            return;

        using var drop = connection.CreateCommand();
        drop.CommandText = "DROP TABLE Customers";
        drop.ExecuteNonQuery();
    }
}
