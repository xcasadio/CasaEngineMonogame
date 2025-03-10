﻿namespace CasaEngine.Framework.AI.Reinforcement_Learning.QLearning;

public class QPolicy
{
    private readonly Dictionary<KeyValuePair<string, string>, float> _qValues = new();







    public float GetQValues(string state, string action)
    {
        return _qValues[new KeyValuePair<string, string>(state, action)];
    }

    public float GetQValues(string state, int numAction)
    {
        var action = 0;

        foreach (var pair in _qValues)
        {
            if (pair.Key.Key.Equals(state))
            {
                //if (pair.Value != 0.0f) //check if action is possible
                if (action == numAction)
                {
                    return pair.Value;
                }

                action++;
            }
        }

        throw new ArgumentException("GetQValues() : numAction_ is too high");
    }

    public void SetQValue(string state, string action, float value)
    {
        _qValues[new KeyValuePair<string, string>(state, action)] = value;
    }

    public int GetNumberOfActions(IQAgent agent, string state)
    {
        var action = 0;

        foreach (var pair in _qValues)
        {
            if (pair.Key.Key.Equals(state) && agent.IsActionIsPossible(pair.Key.Value))
            {
                //if (pair.Value != 0.0f) //check if action is possible
                action++;
            }
        }

        return action;
    }

    public string GetNewStateFromAction(IQAgent agent, string state, int numAction)
    {
        var action = 0;

        foreach (var pair in _qValues)
        {
            if (pair.Key.Key.Equals(state) && agent.IsActionIsPossible(pair.Key.Value))
            {
                if (action == numAction)
                {
                    return pair.Key.Key;
                }

                //if (pair.Value != 0.0f) //check if action is possible
                action++;
            }
        }

        throw new ArgumentException("GetNewStateFromAction() : numAction_ is too high");
    }

    public string GetActionNumber(IQAgent agent, string state, int numAction)
    {
        var action = 0;

        foreach (var pair in _qValues)
        {
            if (pair.Key.Key.Equals(state) && agent.IsActionIsPossible(pair.Key.Value))
            {
                if (action == numAction)
                {
                    return pair.Key.Value;
                }

                //if (pair.Value != 0.0f) //check if action is possible
                action++;
            }
        }

        throw new ArgumentException("GetNewStateFromAction() : numAction_ is too high");
    }

    public void CreateStatesAndActions(KeyValuePair<string, string>[] states, float[] values)
    {
        if (states.Length != values.Length)
        {
            throw new ArgumentException("CreateStatesAndActions() : states_.Length != values_.Length");
        }

        for (var i = 0; i < states.Length; i++)
        {
            _qValues.Add(states[i], values[i]);
        }
    }

}