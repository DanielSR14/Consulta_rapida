using ClienteConsulta.Core.Models;

namespace ClienteConsulta.Core.Abstractions;

/// <summary>
/// Ponto único de acesso aos dados de clientes. Toda a lógica de leitura fica
/// isolada atrás desta interface para que a fonte de dados (hoje uma planilha
/// Excel) possa ser substituída por SQL Server, PostgreSQL, MySQL, uma API REST
/// etc. sem que o restante da aplicação precise mudar.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>Retorna todos os clientes. Implementações devem manter um cache em memória.</summary>
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Força a releitura da fonte de dados, descartando qualquer cache.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
