﻿using System;
using System.Collections.Generic;
using System.Text;
using IotHub.Core.Cqrs;

namespace IotHub.Core.CqrsEngine
{
   public static class CommandEventSender
    {
        public static void Send(ICommand cmd)
        {
            CommandsAndEventsRegisterEngine.PushCommand(cmd);
        }

        public static void Send(IEvent evt)
        {
            CommandsAndEventsRegisterEngine.PushEvent(evt);
        }
    }
}