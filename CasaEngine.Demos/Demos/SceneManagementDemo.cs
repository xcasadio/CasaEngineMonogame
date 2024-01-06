using System.Runtime.InteropServices;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos;

public class SceneManagementDemo : Demo
{
    public override string Title => "Scene management demo";

    public override void Initialize(CasaEngineGame game)
    {
        var root = game.GameManager.CurrentWorld.SceneRoot;
        root.NameString = "Root";

        var scale_xform = MatrixTransform.Create(Matrix.CreateScale(1f));
        scale_xform.NameString = "Scale XForm";


        var boxPrimitive = new BoxPrimitive(game.GraphicsDevice);
        var staticMesh = boxPrimitive.CreateMesh();
        staticMesh.Initialize(game.GraphicsDevice, null);

        staticMesh.Texture = game.GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);

        var geometry = new Geometry<VertexPositionNormalTexture>();
        geometry.Name = "geometry";
        geometry.Mesh = staticMesh;
        /*
        var indices = new uint[staticMesh.IndexBuffer.IndexCount];
        staticMesh.IndexBuffer.GetData(indices);
        geometry.IndexData = indices;

        var vertices = new Microsoft.Xna.Framework.Graphics.VertexPositionNormalTexture[staticMesh.VertexBuffer.VertexCount];
        staticMesh.VertexBuffer.GetData(vertices);
        var vertices2 = new CasaEngine.Demos.Demos.VertexPositionNormalTexture[staticMesh.VertexBuffer.VertexCount];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices2[i].Position = vertices[i].Position;
            vertices2[i].Normal = vertices[i].Normal;
            vertices2[i].TextureCoordinate = vertices[i].TextureCoordinate;
        }
        geometry.VertexData = vertices2;
        */
        var cube = new Geode();
        cube.AddDrawable(geometry);
        scale_xform.AddChild(cube);

        var gridSize = 10;
        var transF = 1f; //2.0f / gridSize;
        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var xform = MatrixTransform.Create(Matrix.CreateTranslation(transF * i, transF * j, 0.0f));
                xform.NameString = $"XForm[{i}, {j}]";
                xform.AddChild(scale_xform);
                root.AddChild(xform);
            }
        }

        /////////////////////////////////////////////////////////////////////////////
        var rootEntity = game.GameManager.CurrentWorld.RootEntity;

        //var boxPrimitive = new BoxPrimitive(game.GraphicsDevice);
        //var staticMesh = boxPrimitive.CreateMesh();
        //staticMesh.Initialize(game.GraphicsDevice, null);



        for (var i = -gridSize; i <= gridSize; ++i)
        {
            for (var j = -gridSize; j <= gridSize; ++j)
            {
                var entity = new Entity();
                entity.Name = $"Cube[{i}, {j}]";
                entity.Coordinates.LocalPosition = new Vector3(transF * i, transF * j, 0.0f);
                var staticMeshComponent = new StaticMeshComponent();
                staticMeshComponent.Mesh = staticMesh;
                entity.ComponentManager.Add(staticMeshComponent);

                rootEntity.Children.Add(entity);
            }
        }

        var sceneGraphComponent = new SceneGraphComponent2(game);
        sceneGraphComponent.Initialize();

        game.GameManager.CurrentWorld.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
    }

    protected override void Clean()
    {

    }
}




[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPositionNormalTexture : IVertexType, IPrimitiveElement
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;

    public Vector3 VertexPosition
    {
        readonly get => Position;
        set => Position = value;
    }

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(new VertexElement[3]
    {
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
    });

    public VertexPositionNormalTexture(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
    {
        this.Position = position;
        this.Normal = normal;
        this.TextureCoordinate = textureCoordinate;
    }

    VertexDeclaration IVertexType.VertexDeclaration => VertexPositionNormalTexture.VertexDeclaration;
}