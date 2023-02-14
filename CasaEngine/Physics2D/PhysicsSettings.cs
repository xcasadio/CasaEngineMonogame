namespace CasaEngine.Physics2D
{
    public class PhysicsSettings
    {
        public PhysicsSettings()
        {
            VelocityIterations = 8;// 10;
            PositionIterations = 3;// 8;
            DrawShapes = 1;
            DrawJoints = 1;
            EnableWarmStarting = 1;
            EnableContinuous = 1;
        }

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
