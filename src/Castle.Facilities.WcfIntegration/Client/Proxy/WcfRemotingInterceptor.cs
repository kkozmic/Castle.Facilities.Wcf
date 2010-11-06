// Copyright 2004-2010 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License

namespace Castle.Facilities.WcfIntegration.Proxy
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Runtime.Remoting.Messaging;

	using Castle.DynamicProxy;

	public class WcfRemotingInterceptor : IWcfInterceptor
	{
		private readonly WcfClientExtension clients;

		public WcfRemotingInterceptor(WcfClientExtension clients)
		{
			this.clients = clients;
		}

		public WcfClientExtension Clients
		{
			get { return clients; }
		}

		public void Intercept(IInvocation invocation)
		{
			var channelHolder = invocation.Proxy as IWcfChannelHolder;

			if (channelHolder == null)
			{
				throw new ArgumentException("The given Proxy is not valid WCF dynamic proxy.");
			}

			PerformInvocation(invocation, channelHolder);
		}

		protected virtual void PerformInvocation(IInvocation invocation, IWcfChannelHolder channelHolder)
		{
			PerformInvocation(channelHolder, invocation, wcfInvocation =>
			{
				var callMessage = new MethodCallMessage(wcfInvocation.Invocation.Method, wcfInvocation.Invocation.Arguments);
				var returnMessage = (IMethodReturnMessage)wcfInvocation.ChannelHolder.RealProxy.Invoke(callMessage);

				if (returnMessage.Exception != null)
				{
					throw returnMessage.Exception;
				}

				wcfInvocation.ReturnValue = returnMessage.ReturnValue;
			});
		}

		bool IWcfInterceptor.Handles(MethodInfo method)
		{
			return Handles(method);
		}

		protected virtual bool Handles(MethodInfo method)
		{
			return true;
		}
		protected void PerformInvocation(IWcfChannelHolder channelHolder, IInvocation invocation, Action<WcfInvocation> action)
		{
			var policies = CollectPolicies(channelHolder);

			var wcfInvocation = new WcfInvocation(channelHolder, invocation);
			InvokeCallPipeline(0, wcfInvocation, policies.ToArray(), action);
			invocation.ReturnValue = wcfInvocation.ReturnValue;
		}

		private ICollection<IWcfPolicy> CollectPolicies(IWcfChannelHolder channelHolder)
		{
			var channelBurden = channelHolder.ChannelBurden;
			var policies = new HashSet<IWcfPolicy>();

			foreach (var policy in channelBurden.Dependencies
				.OfType<IWcfPolicy>().OrderBy(p => p.ExecutionOrder))
			{
				policies.Add(policy);
			}
			if(policies.Count == 0 && clients.DefaultChannelPolicy != null)
			{
				policies.Add(clients.DefaultChannelPolicy);
			}
			return policies;
		}

		private void InvokeCallPipeline(int index, WcfInvocation wcfInvocation, IWcfPolicy[] policies, Action<WcfInvocation> action)
		{
			if (index >= policies.Length)
			{
				action(wcfInvocation);
				return;
			}
			var nextIndex = index + 1;
			wcfInvocation.SetProceedDelegate(() => InvokeCallPipeline(nextIndex, wcfInvocation, policies, action));
			policies[index].Apply(wcfInvocation);
		}
	}
}