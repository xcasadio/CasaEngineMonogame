//-----------------------------------------------------------------------------
// Move.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using System.Text.Json;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Input.InputSequence;

public class Move
{
    public string Name;

    // The sequence of button presses required to activate this move.
    public List<List<InputManager.KeyState>> Sequence = new();

    // Set this to true if the input used to activate this move may
    // be reused as a component of longer moves.
    public bool IsSubMove;

    public Move(string name)
    {
        Name = name;
    }

    public Move(JsonElement element, SaveOption option)
    {
        Load(element, option);
    }

    public bool Match(int index, InputManager.KeyState[] buttons)
    {
        var x = 0;

        for (var keyIndex = 0; keyIndex < Sequence[index].Count; keyIndex++)
        {
            var keyState = Sequence[index][keyIndex];

            for (var buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
            {
                var button = buttons[buttonIndex];
                if (button.Match(keyState))
                {
                    x++;
                    break;
                }
            }
        }

        return x == Sequence[index].Count;
    }

    public void Load(JsonElement element, SaveOption option)
    {
        Name = element.GetProperty("name").GetString();

        foreach (var sequenceElement in element.GetProperty("sequences").EnumerateArray())
        {
            Sequence.Add(new List<InputManager.KeyState>());

            foreach (var buttonElement in sequenceElement.EnumerateArray())
            {
                var keyState = new InputManager.KeyState();
                keyState.Key = buttonElement.GetProperty("key").GetInt32();
                keyState.State = buttonElement.GetProperty("key").GetEnum<ButtonState>();
                keyState.Time = buttonElement.GetProperty("time").GetSingle();
            }
        }
    }

#if EDITOR

    public void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("name", Name);

        var sequencesArray = new JArray();

        foreach (var tab in Sequence)
        {
            var buttonsArray = new JArray();

            foreach (var k in tab)
            {
                var buttonObject = new JObject
                {
                    { "key", k.Key },
                    { "state", k.State.ConvertToString() },
                    { "time", k.Time }
                };
                buttonsArray.Add(buttonObject);
            }

            sequencesArray.Add(buttonsArray);
        }

        jObject.Add("sequences", sequencesArray);
    }

#endif
}