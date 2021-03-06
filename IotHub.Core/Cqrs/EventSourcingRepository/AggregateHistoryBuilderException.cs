﻿using System;
using System.Runtime.Serialization;

namespace IotHub.Core.Cqrs.EventSourcingRepository
{
    public class AggregateHistoryBuilderException : Exception
    {
        public AggregateHistoryBuilderException()
        {
        }

        public AggregateHistoryBuilderException(string message) : base(message)
        {
        }

        public AggregateHistoryBuilderException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AggregateHistoryBuilderException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}