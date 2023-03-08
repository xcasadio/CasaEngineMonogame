namespace CasaEngine.Framework.Entities;

public interface IExternalComponent
{
    public string Name { get; }

    public int Id { get; }

    public void Initialize();

    public void Update(float elapsedTime);

    public void Draw();
}