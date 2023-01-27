using System;

namespace CasaEngineCommon.Design
{
	/// <summary>
    /// Can notify IObserver (Observer pattern)
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface IObservable<T>
	{
		void RegisterObserver(IObserver<T> arg_);
		void UnRegisterObserver(IObserver<T> arg_);
		void NotifyObservers();
	}

}