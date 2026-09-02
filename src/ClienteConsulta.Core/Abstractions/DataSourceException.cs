namespace ClienteConsulta.Core.Abstractions;

/// <summary>Erro esperado de leitura da fonte de dados (arquivo ausente, coluna faltando, etc.), com mensagem já adequada para exibir ao usuário.</summary>
public sealed class DataSourceException : Exception
{
    public DataSourceException(string message) : base(message) { }
    public DataSourceException(string message, Exception innerException) : base(message, innerException) { }
}
