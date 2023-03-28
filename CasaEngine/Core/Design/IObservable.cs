namespace CasaEngine.Core.Design;

public interface IObservable<T>
{
    void RegisterObserver(IObserver<T> arg);
    void UnRegisterObserver(IObserver<T> arg);
    void NotifyObservers();
}