using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Transactions;
using FubuTestingSupport;
using LightningQueues.Model;
using NUnit.Framework;

namespace LightningQueues.Tests.FromUsers
{
    [TestFixture]
    public class FromRene
	{
		QueueManager receiver;
		private volatile bool keepRunning = true;
		private readonly List<string> msgs = new List<string>();

        [SetUp]
		public void Setup()
		{
			using (var tx = new TransactionScope())
			{
			    receiver = ObjectMother.QueueManager("receiver", 4545, "uno");
                receiver.Start();

				tx.Complete();
			}
		}

		public void Sender(int count)
		{
			using (var sender = ObjectMother.QueueManager("sender", 4546))
			{
                sender.Start();
				using (var tx = new TransactionScope())
				{
					sender.Send(new Uri("lq.tcp://localhost:4545/uno"),
					            new MessagePayload
					            {
					            	Data = Encoding.ASCII.GetBytes("Message "+count)
					            }
						);
					tx.Complete();
				}
				sender.WaitForAllMessagesToBeSent();
			}
		}

		public void Receiver(object ignored)
		{
			while (keepRunning)
			{
				using (var tx = new TransactionScope())
				{
					Message msg;
					try
					{
						msg = receiver.Receive("uno", null, new TimeSpan(0, 0, 10));
					}
					catch (TimeoutException)
					{
						continue;
					}
					catch(ObjectDisposedException)
					{
						continue;
					}
					lock (msgs)
					{
						msgs.Add(Encoding.ASCII.GetString(msg.Data));
						Console.WriteLine(msgs.Count);
					}
					tx.Complete();
				}
			}
		}

		[Test]
		public void ShouldOnlyGetTwoItems()
		{
			ThreadPool.QueueUserWorkItem(Receiver);

			Sender(4);

			Sender(5);

			while(true)
			{
				lock (msgs)
				{
					if (msgs.Count>1)
						break;
				}
				Thread.Sleep(100);
			}
			Thread.Sleep(2000);//let it try to do something in addition to that
			receiver.Dispose();
			keepRunning = false;

			2.ShouldEqual(msgs.Count);
			"Message 4".ShouldEqual(msgs[0]);
			"Message 5".ShouldEqual(msgs[1]);
		}

        [TearDown]
		public void Teardown()
		{
			receiver.Dispose();
		}
	}
}
