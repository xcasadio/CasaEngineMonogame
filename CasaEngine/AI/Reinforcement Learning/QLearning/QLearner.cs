using CasaEngine.Gameplay.Actor.Object;


namespace CasaEngine.AI.Reinforcement_Learning.QLearning
{
    public class QLearner
        : BaseObject
    {
        readonly QLearning m_QL = new QLearning();



        public QLearning QLearning => m_QL;


        public void Update(float dt)
        {

            //string newState = string.Empty;
            //m_QL.Learn(state_, action_, newState);
            //m_QL.Learn()
        }

        public void Update(IQAgent agent_, string currentState_)
        {
            m_QL.Learn(agent_, currentState_);
        }

#if EDITOR

        public override bool CompareTo(BaseObject other_)
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
