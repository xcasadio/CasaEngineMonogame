using System.Diagnostics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public class SceneGraphComponent : GameComponent
{
    private readonly UpdateVisitor _updateVisitor;
    private readonly CullVisitor _cullVisitor;

    private readonly Stopwatch _stopWatch = new();
    private CasaEngineGame _game;
    private StaticMeshRendererComponent? _staticMeshRendererComponent;

    public SceneGraphComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _updateVisitor = new UpdateVisitor();
        _cullVisitor = new CullVisitor();

        _game = game as CasaEngineGame;

        UpdateOrder = (int)ComponentUpdateOrder.ScreenManagerComponent;
        _game.Components.Add(this);
    }

    public override void Initialize()
    {
        _staticMeshRendererComponent = Game.GetDrawableGameComponent<StaticMeshRendererComponent>();
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        _stopWatch.Reset();
        _stopWatch.Start();

        var sceneRootNode = _game.GameManager.CurrentWorld.SceneRoot;
        Update(sceneRootNode);

        var postUpdate = _stopWatch.ElapsedMilliseconds;

        var camera = _game.GameManager.ActiveCamera;
        Cull(sceneRootNode, camera.ViewMatrix, camera.ProjectionMatrix);

        var postCull = _stopWatch.ElapsedMilliseconds;

        Record(Game.GraphicsDevice);

        var postRecord = _stopWatch.ElapsedMilliseconds;

        /*_logger.LogTrace(string.Format("Update = {0} ms, Cull = {1} ms, Record = {2}, Draw = {3} ms, Swap = {4} ms",
            postUpdate,
            postCull - postUpdate,
            postRecord - postCull,
            postDraw - postRecord,
            postSwap - postDraw));*/
    }

    private void Update(IGroup sceneRootNode)
    {
        sceneRootNode.Accept(_updateVisitor);
    }

    private void Cull(IGroup sceneRootNode, Matrix viewMatrix, Matrix projectionMatrix)
    {
        _cullVisitor.Reset();
        _cullVisitor.SetView(viewMatrix, projectionMatrix);
        sceneRootNode.Accept(_cullVisitor);
    }

    private void Record(GraphicsDevice device)
    {
        var viewProjectionMatrix = _game.GameManager.ActiveCamera.ViewMatrix * _game.GameManager.ActiveCamera.ProjectionMatrix;

        foreach (var pair in _cullVisitor.Meshes)
        {
            var material = new Material();
            material.TextureBaseColor = pair.Item1.Texture.Resource;
            _staticMeshRendererComponent.AddMesh(pair.Item1, material, pair.Item2,
                pair.Item2 * viewProjectionMatrix, _game.GameManager.ActiveCamera.Position);
        }
    }
}