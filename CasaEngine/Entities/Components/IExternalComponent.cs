namespace CasaEngine.Entities.Components;

public interface IExternalComponent
{
    public string Name { get; }
    public int Id { get; }

    public void Initialize();

    public void Update(float elapsedTime);

    public void Draw();
}