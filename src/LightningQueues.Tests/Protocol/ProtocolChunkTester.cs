﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using FubuCore.Logging;
using FubuTestingSupport;
using LightningQueues.Exceptions;
using LightningQueues.Model;
using LightningQueues.Protocol;
using LightningQueues.Protocol.Chunks;
using NUnit.Framework;

namespace LightningQueues.Tests.Protocol
{
    [TestFixture]
    public class ProtocolChunkTester
    {
        [Test]
        public void write_serialization_error()
        {
            var result = processChunk(new WriteSerializationError(new RecordingLogger()));
            result.ShouldEqual(ProtocolConstants.SerializationFailureBuffer);
        }

        [Test]
        public void write_revert()
        {
            var result = processChunk(new WriteRevert(new RecordingLogger()));
            result.ShouldEqual(ProtocolConstants.RevertBuffer);
        }

        [Test]
        public void write_received()
        {
            var result = processChunk(new WriteReceived(new RecordingLogger()));
            result.ShouldEqual(ProtocolConstants.RecievedBuffer);
        }

        [Test]
        public void write_processing_error()
        {
            var result = processChunk(new WriteProcessingError(new RecordingLogger(), ProtocolConstants.ProcessingFailureBuffer));
            result.ShouldEqual(ProtocolConstants.ProcessingFailureBuffer);
        }

        [Test]
        public void write_message()
        {
            var message = new byte[] { 1, 2, 5 };
            var result = processChunk(new WriteMessage(new RecordingLogger(), message));
            result.ShouldEqual(message);
        }

        [Test]
        public void write_length()
        {
            const int length = 5;
            var result = processChunk(new WriteLength(new RecordingLogger(), length));
            length.ShouldEqual(BitConverter.ToInt32(result, 0));
        }

        [Test]
        public void write_acknowledgement()
        {
            var result = processChunk(new WriteAcknowledgement(new RecordingLogger()));
            result.ShouldEqual(ProtocolConstants.AcknowledgedBuffer);
        }

        [Test]
        public void read_message()
        {
            var message = new Message();
            message.Data = new byte[] {1, 2, 3, 4};
            message.Id = MessageId.GenerateRandom();
            message.Headers.Add("fake", "fakevalue");
            message.Queue = "myqueue";
            message.SentAt = DateTime.Now;
            var messageBytes = new[] {message}.Serialize();

            var messages = getChunk<ReadMessage, Message[]>(new ReadMessage(new RecordingLogger(), messageBytes.Length), new MemoryStream(messageBytes));
            var afterSerialization = messages[0];
            afterSerialization.Data.ShouldEqual(message.Data);
            afterSerialization.Id.ShouldEqual(message.Id);
            afterSerialization.Queue.ShouldEqual(message.Queue);
            afterSerialization.SentAt.ShouldEqual(message.SentAt);
            afterSerialization.Headers.ShouldEqual(message.Headers);
        }

        [Test]
        public void read_message_fails_to_deserialize_throws()
        {
            var ms = new MemoryStream(Encoding.Unicode.GetBytes("Fail!"));
            expectException<SerializationException>(() => getChunk<ReadMessage, Message[]>(new ReadMessage(new RecordingLogger(), 3), ms));
        }

        [Test]
        public void read_acknowledgement()
        {
            var ms = new MemoryStream(ProtocolConstants.AcknowledgedBuffer);
            processChunk(new ReadAcknowledgement(new RecordingLogger()), ms);
            //nothing to assert, but should not throw or hang
        }

        [Test]
        public void read_acknowledgement_and_format_is_unexpected_should_throw()
        {
            var ms = new MemoryStream(Encoding.Unicode.GetBytes("Acknowledges"));
            processChunkWithExpectedErrors<ReadAcknowledgement, InvalidAcknowledgementException>(new ReadAcknowledgement(new RecordingLogger()), ms);
        }

        [Test]
        public void read_received()
        {
            var ms = new MemoryStream(ProtocolConstants.RecievedBuffer);
            processChunk(new ReadReceived(new RecordingLogger()), ms);
            //nothing to assert, but should not throw or hang
        }

        [Test]
        public void read_received_and_format_is_unexpected_should_throw()
        {
            var ms = new MemoryStream(Encoding.Unicode.GetBytes("Reciever"));
            processChunkWithExpectedErrors<ReadReceived, UnexpectedReceivedMessageFormatException>(new ReadReceived(new RecordingLogger()), ms);
        }

        [Test]
        public void read_received_but_queue_doesn_not_exist_chunk()
        {
            var ms = new MemoryStream(ProtocolConstants.QueueDoesNoExiststBuffer);
            processChunkWithExpectedErrors<ReadReceived, QueueDoesNotExistsException>(new ReadReceived(new RecordingLogger()), ms);
        }

        private byte[] processChunk<TChunk>(TChunk chunkWriter, MemoryStream ms = null) where TChunk : Chunk
        {
            ms = ms ?? new MemoryStream();
            var task = chunkWriter.ProcessAsync(ms);
            task.Wait();
            return ms.ToArray();
        }

        private TResult getChunk<TChunk, TResult>(TChunk chunk, MemoryStream ms) where TChunk : Chunk<TResult>
        {
            var task = chunk.GetAsync(ms);
            task.Wait();
            return task.Result;
        }

        private void processChunkWithExpectedErrors<TChunk, TException>(TChunk chunk, MemoryStream ms = null)
            where TChunk : Chunk
            where TException : Exception
        {
            expectException<TException>(() => processChunk(chunk, ms));
        }

        private void expectException<TException>(Action action) where TException : Exception
        {
            bool threwError = false;
            try
            {
                action();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions.OfType<TException>().Any())
                    threwError = true;
            }
            catch (TException)
            {
                threwError = true;
            }
            threwError.ShouldBeTrue();
        }
    }
}