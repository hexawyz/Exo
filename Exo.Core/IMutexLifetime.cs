namespace Exo;

public interface IMutexLifetime
{
	void OnAfterAcquire();
	void OnBeforeRelease();
}
