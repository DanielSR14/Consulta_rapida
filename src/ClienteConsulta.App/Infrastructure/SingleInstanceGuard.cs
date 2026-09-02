using System.Threading;

namespace ClienteConsulta.App.Infrastructure;

/// <summary>
/// Garante uma única instância em execução. Se o app já estiver rodando e o
/// usuário tentar abrir outra cópia (ex: atalho na área de trabalho), sinalizamos
/// a instância existente para trazer a janela de pesquisa para frente, em vez de
/// simplesmente recusar a segunda instância sem explicação.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "ConsultaRapida.SingleInstance.Mutex";
    private const string SignalName = "ConsultaRapida.SingleInstance.ShowRequested";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSignal;
    private readonly bool _isFirstInstance;
    private Thread? _listenerThread;
    private volatile bool _stop;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        _isFirstInstance = createdNew;
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
    }

    public bool IsFirstInstance => _isFirstInstance;

    /// <summary>Sinaliza a instância já em execução para exibir a janela de pesquisa.</summary>
    public void NotifyExistingInstance() => _showSignal.Set();

    /// <summary>Chamado pela instância "dona": passa a escutar pedidos de exibição de outras tentativas de abertura.</summary>
    public void ListenForShowRequests(Action onShowRequested)
    {
        _listenerThread = new Thread(() =>
        {
            while (!_stop)
            {
                if (_showSignal.WaitOne(500))
                    onShowRequested();
            }
        })
        {
            IsBackground = true
        };
        _listenerThread.Start();
    }

    public void Dispose()
    {
        _stop = true;
        if (_isFirstInstance)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
        _showSignal.Dispose();
    }
}
