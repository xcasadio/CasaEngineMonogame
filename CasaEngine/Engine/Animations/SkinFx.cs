using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Engine.Animations;

public class DirectionLight
{
    public Vector3 direction;
    public Vector3 diffuseColor;
    public Vector3 specularColor;
}

public class SkinFx
{
    public const int MAX_BONES = 180; // This should match number in custom SkinEffect.fx
    public const int WEIGHTS_PER_VERTEX = 4;

    public Effect fx;
    public Texture2D default_tex;
    public Vector4 diffuseCol = Vector4.One;
    public Vector3 emissiveCol = Vector3.Zero;
    public Vector3 specularCol = Color.LightYellow.ToVector3();
    public float specularPow = 32f;
    public Vector3 ambientCol = Vector3.Zero;
    public bool fogEnabled = false;
    public DirectionLight[] lights; // lights: key, fill, back 
    public float alpha = 1f;
    public float fogStart = 0f;
    public float fogEnd = 1f;
    //public Matrix world = Matrix.Identity;
    public Matrix worldView = Matrix.Identity;



    //------------------
    // C O N S T R U C T
    //------------------
    public SkinFx(AssetContentManager Content, Effect effect, bool enableFog = false)
    {
        lights = new DirectionLight[3];
        for (int i = 0; i < 3; i++) lights[i] = new DirectionLight();
        fx = effect;
        default_tex = Content.GetAsset<Texture>(Texture.DefaultTextureName).Resource;
        Matrix[] identityBones = new Matrix[MAX_BONES];
        for (int i = 0; i < MAX_BONES; i++)
        {
            identityBones[i] = Matrix.Identity;
        }

        SetBoneTransforms(identityBones);
        fx.Parameters["TexDiffuse"].SetValue(default_tex);
        fx.Parameters["DiffuseColor"].SetValue(diffuseCol);
        fx.Parameters["EmissiveColor"].SetValue(emissiveCol);
        fx.Parameters["SpecularColor"].SetValue(specularCol);
        fx.Parameters["SpecularPower"].SetValue(specularPow);

        SetDefaultLighting();
        if (enableFog)
        {
            ToggleFog();
        }
    }



    //--------------------------------------
    // S E T   B O N E   T R A N S F O R M S 
    //--------------------------------------
    /// <summary> Sets an array of skinning bone transform matrices. </summary>
    public void SetBoneTransforms(Matrix[] boneTransforms)
    {
        if ((boneTransforms == null) || (boneTransforms.Length == 0))
        {
            throw new ArgumentNullException("boneTransforms");
        }

        if (boneTransforms.Length > MAX_BONES)
        {
            throw new ArgumentException();
        }

        fx.Parameters["Bones"].SetValue(boneTransforms);
    }



    //----------------------------------------------
    // S E T   D E F A U L T   L I G H T I N G
    //----------------------------------------------
    public void SetDefaultLighting()
    {
        float u = Vector3.Up.Y; //cam.up.Y;     // I assume up is -Y or +Y
        ambientCol = new Vector3(0.05333332f, 0.09882354f, 0.1819608f);
        lights[0].direction = new Vector3(-0.5265408f, -0.5735765f * u, -0.6275069f); // Key light.
        lights[0].diffuseColor = new Vector3(1, 0.9607844f, 0.8078432f);
        lights[0].specularColor = new Vector3(1, 0.9607844f, 0.8078432f);
        lights[1].direction = new Vector3(0.7198464f, 0.3420201f * u, 0.6040227f); // Fill light
        lights[1].diffuseColor = new Vector3(0.9647059f, 0.7607844f, 0.4078432f);
        lights[1].specularColor = Vector3.Zero;
        lights[2].direction = new Vector3(0.4545195f, -0.7660444f * u, 0.4545195f); // Back light
        lights[2].diffuseColor = new Vector3(0.3231373f, 0.3607844f, 0.3937255f);
        lights[2].specularColor = new Vector3(0.3231373f, 0.3607844f, 0.3937255f);
        fx.Parameters["LightDir1"].SetValue(lights[0].direction);
        fx.Parameters["LightDiffCol1"].SetValue(lights[0].diffuseColor);
        fx.Parameters["LightSpecCol1"].SetValue(lights[0].specularColor);
        fx.Parameters["LightDir2"].SetValue(lights[1].direction);
        fx.Parameters["LightDiffCol2"].SetValue(lights[1].diffuseColor);
        fx.Parameters["LightSpecCol2"].SetValue(lights[1].specularColor);
        fx.Parameters["LightDir3"].SetValue(lights[2].direction);
        fx.Parameters["LightDiffCol3"].SetValue(lights[2].diffuseColor);
        fx.Parameters["LightSpecCol3"].SetValue(lights[2].specularColor);
    }



    // S E T   D I R E C T I O N A L   L I G H T 
    public void SetDirectionalLight(int index, Vector3 direction, Color diffuse_color, Color specular_color)
    {
        if (index >= 3)
        {
            return;
        }

        lights[index].direction = direction;
        lights[index].diffuseColor = diffuse_color.ToVector3();
        lights[index].specularColor = specular_color.ToVector3();
        switch (index)
        {
            case 0:
                fx.Parameters["LightDir1"].SetValue(lights[0].direction);
                fx.Parameters["LightDiffCol1"].SetValue(lights[0].diffuseColor);
                fx.Parameters["LightSpecCol1"].SetValue(lights[0].specularColor);
                break;
            case 1:
                fx.Parameters["LightDir2"].SetValue(lights[1].direction);
                fx.Parameters["LightDiffCol2"].SetValue(lights[1].diffuseColor);
                fx.Parameters["LightSpecCol2"].SetValue(lights[1].specularColor);
                break;
            case 2:
                fx.Parameters["LightDir3"].SetValue(lights[2].direction);
                fx.Parameters["LightDiffCol3"].SetValue(lights[2].diffuseColor);
                fx.Parameters["LightSpecCol3"].SetValue(lights[2].specularColor);
                break;
        }
    }



    public void SetFogStart(float fog_start)
    {
        fogEnabled = false;
        fogStart = fog_start;
        ToggleFog();
    }

    public void SetFogEnd(float fog_end)
    {
        fogEnabled = false;
        fogEnd = fog_end;
        ToggleFog();
    }

    public void SetFogColor(Color fog_color)
    {
        fx.Parameters["FogColor"].SetValue(fog_color.ToVector3());
    }

    // T O G G L E   F O G 
    public void ToggleFog()
    {
        if (!fogEnabled)
        {
            if (fogStart == fogEnd)
            {
                fx.Parameters["FogVector"].SetValue(new Vector4(0, 0, 0, 1));
            }
            else
            {
                // We want to transform vertex positions into view space, take the resulting Z value, then scale and offset according to the fog start/end distances.
                // Because we only care about the Z component, the shader can do all this with a single dot product, using only the Z row of the world+view matrix.
                float scale = 1f / (fogStart - fogEnd);
                Vector4 fogVector = new Vector4();
                fogVector.X = worldView.M13 * scale;
                fogVector.Y = worldView.M23 * scale;
                fogVector.Z = worldView.M33 * scale;
                fogVector.W = (worldView.M43 + fogStart) * scale;
                fx.Parameters["FogVector"].SetValue(fogVector);
                fogEnabled = true;
            }
        }
        else
        {
            fx.Parameters["FogVector"].SetValue(Vector4.Zero);
            fogEnabled = false;
        }
    }



    //------------------------------
    // S E T   D R A W   P A R A M S (use just before drawing)
    //------------------------------
    public void SetDrawParams(Matrix world, Matrix viewProjMatrix, Matrix viewMatrix, Vector3 cameraPosition,
        Texture2D texture = null, Texture2D normalMapTex = null, Texture2D specularTex = null)
    {
        Matrix.Multiply(ref world, ref viewMatrix, out worldView); // (used with fog)
        Matrix worldInverse, worldInverseTranspose;
        Matrix.Invert(ref world, out worldInverse);
        Matrix.Transpose(ref worldInverse, out worldInverseTranspose);
        Vector4 diffuse = new Vector4();
        Vector3 emissive = new Vector3();
        diffuse.X = diffuseCol.X * alpha;
        diffuse.Y = diffuseCol.Y * alpha;
        diffuse.Z = diffuseCol.Z * alpha;
        diffuse.W = alpha;
        emissive.X = (emissiveCol.X + ambientCol.X * diffuseCol.X) * alpha;
        emissive.Y = (emissiveCol.Y + ambientCol.Y * diffuseCol.Y) * alpha;
        emissive.Z = (emissiveCol.Z + ambientCol.Z * diffuseCol.Z) * alpha;
        fx.Parameters["World"].SetValue(world);
        fx.Parameters["WorldViewProj"].SetValue(world * viewProjMatrix);
        fx.Parameters["CamPos"].SetValue(cameraPosition);
        if (texture != null)
        {
            fx.Parameters["TexDiffuse"].SetValue(texture);
        }
        else
        {
            fx.Parameters["TexDiffuse"].SetValue(default_tex);
        }

        //if (specularTex  != null) fx.Parameters["TexSpecular"].SetValue(specularTex);
        fx.Parameters["WorldInverseTranspose"].SetValue(worldInverseTranspose);
        fx.Parameters["DiffuseColor"].SetValue(diffuse);
        fx.Parameters["EmissiveColor"].SetValue(emissive);
        if (normalMapTex == null)
        {
            fx.CurrentTechnique = fx.Techniques["Skin_Directional_Fog"];
        }
        else
        {
            fx.Parameters["TexNormalMap"].SetValue(normalMapTex);
            fx.CurrentTechnique = fx.Techniques["Skin_NormalMapped_Directional_Fog"];
        }

        fx.CurrentTechnique.Passes[0].Apply();
    }

    public void SetDiffuseCol(Vector4 diffuse)
    {
        diffuseCol = diffuse;
    }

    public void SetEmissiveCol(Vector3 emissive)
    {
        emissiveCol = emissive;
    }

    public void SetSpecularCol(Vector3 specular)
    {
        specularCol = specular;
        fx.Parameters["SpecularColor"].SetValue(specularCol);
    }

    public void SetSpecularPow(float power)
    {
        specularPow = power;
        fx.Parameters["SpecularPower"].SetValue(power);
    }

    public void SetShineAmplify(float amp)
    {
        // currently using to make eyes more shiny (triggered by low alpha)    
        fx.Parameters["Shine_Amplify"].SetValue(amp);
    }

}