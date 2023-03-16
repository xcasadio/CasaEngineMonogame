using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics2D
{
    public class Physics2dSettings
    {
        public Physics2dSettings()
        {
            VelocityIterations = 8;// 10;
            PositionIterations = 3;// 8;
            EnableWarmStarting = 1;
            EnableContinuous = 1;
            //debugging
            DrawShapes = 1;
            DrawJoints = 1;
        }

        public Vector2 Gravity { get; set; }

        public int VelocityIterations;
        public int PositionIterations;
        public uint DrawShapes;
        public uint DrawJoints;
        public uint DrawAABBs;
        public uint DrawPairs;
        public uint DrawContactPoints;
        public uint DrawContactNormals;
        public uint DrawContactForces;
        public uint DrawFrictionForces;
        public uint DrawCOMs;
        public uint DrawStats;
        public uint EnableWarmStarting;
        public uint EnableContinuous;
    }
}
