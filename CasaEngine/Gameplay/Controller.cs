using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.AI.StateMachines;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class Controller
        : IFSMCapable<Controller>
    {
        #region Fields

        private FiniteStateMachine<Controller> m_FSM;
        private Dictionary<int, IState<Controller>> m_States = new Dictionary<int, IState<Controller>>();
        private CharacterActor2D m_Character;

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public IFiniteStateMachine<Controller> StateMachine
        {
            get { return m_FSM; }
            set { throw new NotImplementedException(); }
        }

		/// <summary>
		/// 
		/// </summary>
        public CharacterActor2D Character
		{
			get { return m_Character; }
			set { m_Character = value; }
		}

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fighter_"></param>
        protected Controller(CharacterActor2D character_)
		{
            m_FSM = new FiniteStateMachine<Controller>(this);
			Character = character_;
		}

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateId_"></param>
        /// <returns></returns>
        public IState<Controller> GetState(int stateId_)
        {
            return m_States[stateId_];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateId_"></param>
        /// <param name="state_"></param>
        /// <returns></returns>
        public void AddState(int stateId_, IState<Controller> state_)
        {
            m_States.Add(stateId_, state_);
        }

        /// <summary>
        /// 
        /// </summary>
        public abstract void Initialize();
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public virtual void Update(float elapsedTime_)
        {
            m_FSM.Update(elapsedTime_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /*public override string ToString()
        {
            return m_FSM.CurrentState.ToString();
        }*/

        #endregion
    }
}
