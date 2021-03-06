﻿using IotHub.Core.Api;
using IotHub.Core.Cqrs;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Net;

namespace IotHub.Core.CqrsEngine
{
    [Route("api/CommandSender")]
    [ApiController]
    public class CommandSenderController : JsonControllerBase
    {
        [HttpPost]
        public CommandResponse Post(CommandRequest cmd)
        {
            try
            {
                var jobj = JsonConvert.DeserializeObject(cmd.CommandDataJson) as Newtonsoft.Json.Linq.JObject;

                var objectType = CommandsAndEventsRegisterEngine.FindTypeOfCommandOrEvent(cmd.CommandTypeFullName);
                if (objectType == null || 
                    jobj == null)
                {
                    return new CommandResponse()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotImplemented,
                        Message = "Not found command type",
                        CommandId = cmd.CommandId
                    };
                }

                var o = jobj.ToObject(objectType);

                CommandEventSender.Send((ICommand)o, cmd.TokenSession);

                return new CommandResponse()
                {
                    Success = true,
                    CommandId = cmd.CommandId,
                    Message = "Success",
                    StatusCode = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new CommandResponse()
                {
                    CommandId = cmd.CommandId,
                    Message = ex.Message,
                    StatusCode = HttpStatusCode.BadGateway,
                    Success = false
                };
            }

        }
    }

    public class CommandRequest
    {
        public Guid CommandId { get; set; }
        public string CommandTypeFullName { get; set; }
        public string CommandDataJson { get; set; }
        public string TokenSession { get; set; }
    }

    public class CommandResponse:BaseResponse
    {
        public Guid CommandId { get; set; }
    }
}
