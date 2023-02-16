using CasaEngine.Entities;

namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
    public class QLearner
        : Entity
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
    }
}
