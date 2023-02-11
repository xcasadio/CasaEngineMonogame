using CasaEngine.Gameplay.Actor.Object;


namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
    public class QLearner
        : BaseObject
    {
        readonly QLearning _ql = new();



        public QLearning QLearning => _ql;


        public void Update(float dt)
        {

            //string newState = string.Empty;
            //_QL.Learn(state_, action_, newState);
            //_QL.Learn()
        }

        public void Update(IQAgent agent, string currentState)
        {
            _ql.Learn(agent, currentState);
        }

#if EDITOR

        public override bool CompareTo(BaseObject other)
        {
            throw new Exception("The method or operation is not implemented.");
        }

#endif

        public override BaseObject Clone()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
