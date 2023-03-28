namespace CasaEngine.Core.Design;

/// <summary>
/// Class notified when T changed (Observer pattern)
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IObserver<T>
{
    void OnNotify(T arg_);
    void OnError(Exception ex_);
    void OnUnregister(T object_);
}