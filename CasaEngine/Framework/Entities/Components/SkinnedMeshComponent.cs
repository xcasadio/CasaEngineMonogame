﻿using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Skinned Mesh")]
public class SkinnedMeshComponent : Component, IBoundingBoxComputable
{
    public static readonly int ComponentId = (int)ComponentIds.SkinnedMesh;
    private SkinnedMeshRendererComponent? _skinnedMeshRendererComponent;
    private RiggedModel? _skinnedMesh;

    //TODO remove : use only in editor mode to retrieve the game. Very ugly....
    public CasaEngineGame Game { get; private set; }

    public RiggedModel? SkinnedMesh
    {
        get { return _skinnedMesh; }
        set
        {
            _skinnedMesh = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public SkinnedMeshComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize(CasaEngineGame game)
    {
        Game = game;
        _skinnedMeshRendererComponent = game.GetGameComponent<SkinnedMeshRendererComponent>();
        //Mesh?.Initialize(game.GraphicsDevice);
        //Mesh?.Texture?.Initialize(game.GraphicsDevice, game.GameManager.AssetContentManager);
    }

    public override void Update(float elapsedTime)
    {
        if (SkinnedMesh == null)
        {
            return;
        }

        SkinnedMesh.Update(elapsedTime);

        var camera = Game.GameManager.ActiveCamera;
        _skinnedMeshRendererComponent.AddMesh(
            SkinnedMesh,
            Owner.Coordinates.WorldMatrix,
            camera.ViewMatrix,
            camera.ProjectionMatrix,
            camera.Position);
    }

    public override Component Clone(Entity owner)
    {
        var component = new SkinnedMeshComponent(owner);

        component._skinnedMesh = _skinnedMesh;

        return component;
    }

    public override void Load(JsonElement element)
    {
        var meshElement = element.GetProperty("skinned_mesh");

        if (meshElement.ToString() != "null")
        {
            //_mesh = new SkinModel();
            //_mesh.Load(meshElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        JObject newJObject = new();
        //if (SkinnedMesh != null)
        //{
        //    SkinnedMesh.Save(newJObject);
        //    jObject.Add("skinned_mesh", newJObject);
        //}
        //else
        //{
        //    jObject.Add("mesh", "null");
        //}
    }

    public BoundingBox BoundingBox
    {
        get
        {
            var min = Vector3.One * int.MaxValue;
            var max = Vector3.One * int.MinValue;

            if (SkinnedMesh != null)
            {
                //var vertices = SkinnedMesh.GetVertices();
                //
                //foreach (var vertex in vertices)
                //{
                //    min = Vector3.Min(min, vertex.Position);
                //    max = Vector3.Max(max, vertex.Position);
                //}
            }
            else // default box
            {
                const float length = 0.5f;
                min = Vector3.One * -length;
                max = Vector3.One * length;
            }

            min = Vector3.Transform(min, Owner.Coordinates.WorldMatrix);
            max = Vector3.Transform(max, Owner.Coordinates.WorldMatrix);

            return new BoundingBox(min, max);
        }
    }
#endif
}