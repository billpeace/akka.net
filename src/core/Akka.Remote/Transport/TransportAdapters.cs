﻿//-----------------------------------------------------------------------
// <copyright file="TransportAdapters.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Internal;
using Akka.Event;

namespace Akka.Remote.Transport
{
    /// <summary>
    /// TBD
    /// </summary>
    public interface ITransportAdapterProvider
    {
        /// <summary>
        /// Create a transport adapter that wraps the underlying transport
        /// </summary>
        /// <param name="wrappedTransport">TBD</param>
        /// <param name="system">TBD</param>
        /// <returns>TBD</returns>
        Transport Create(Transport wrappedTransport, ExtendedActorSystem system);
    }

    /// <summary>
    /// TBD
    /// </summary>
    internal class TransportAdaptersExtension : ExtensionIdProvider<TransportAdapters>
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="system">TBD</param>
        /// <returns>TBD</returns>
        public override TransportAdapters CreateExtension(ExtendedActorSystem system)
        {
            return new TransportAdapters((ActorSystemImpl) system);
        }

        #region Static methods

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="system">TBD</param>
        /// <returns>TBD</returns>
        public static TransportAdapters For(ActorSystem system)
        {
            return system.WithExtension<TransportAdapters, TransportAdaptersExtension>();
        }

        #endregion
    }

    /// <summary>
    /// INTERNAL API
    /// 
    /// Extension that allows us to look up transport adapters based upon the settings provided inside <see cref="RemoteSettings"/>
    /// </summary>
    internal class TransportAdapters : IExtension
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="system">TBD</param>
        public TransportAdapters(ExtendedActorSystem system)
        {
            System = system;
            Settings = ((RemoteActorRefProvider)system.Provider).RemoteSettings;
        }

        /// <summary>
        /// TBD
        /// </summary>
        public ActorSystem System { get; private set; }

        /// <summary>
        /// TBD
        /// </summary>
        protected RemoteSettings Settings;

        private Dictionary<string, ITransportAdapterProvider> _adaptersTable;

        private Dictionary<string, ITransportAdapterProvider> AdaptersTable()
        {
            if (_adaptersTable != null) return _adaptersTable;
            _adaptersTable = new Dictionary<string, ITransportAdapterProvider>();
            foreach (var adapter in Settings.Adapters)
            {
                try
                {
                    var adapterTypeName = Type.GetType(adapter.Value);
                    // ReSharper disable once AssignNullToNotNullAttribute
                    var newAdapter = (ITransportAdapterProvider)Activator.CreateInstance(adapterTypeName);
                    _adaptersTable.Add(adapter.Key, newAdapter);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Cannot initiate transport adapter {adapter.Value}", ex);
                }
            }

            return _adaptersTable;
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="name">TBD</param>
        /// <exception cref="ArgumentException">TBD</exception>
        /// <returns>TBD</returns>
        public ITransportAdapterProvider GetAdapterProvider(string name)
        {
            if (AdaptersTable().TryGetValue(name, out var provider))
                return provider;

            throw new ArgumentException($"There is no registered transport adapter provider with name {name}");
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    public class SchemeAugmenter
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="addedSchemeIdentifier">TBD</param>
        public SchemeAugmenter(string addedSchemeIdentifier)
        {
            AddedSchemeIdentifier = addedSchemeIdentifier;
        }

        /// <summary>
        /// TBD
        /// </summary>
        public readonly string AddedSchemeIdentifier;

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="originalScheme">TBD</param>
        /// <returns>TBD</returns>
        public string AugmentScheme(string originalScheme)
        {
            return string.Format("{0}.{1}", AddedSchemeIdentifier, originalScheme);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="address">TBD</param>
        /// <returns>TBD</returns>
        public Address AugmentScheme(Address address)
        {
            var protocol = AugmentScheme(address.Protocol);
            return address.WithProtocol(protocol);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="scheme">TBD</param>
        /// <returns>TBD</returns>
        public string RemoveScheme(string scheme)
        {
            if (scheme.StartsWith(string.Format("{0}.", AddedSchemeIdentifier)))
                return scheme.Remove(0, AddedSchemeIdentifier.Length + 1);
            return scheme;
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="address">TBD</param>
        /// <returns>TBD</returns>
        public Address RemoveScheme(Address address)
        {
            var protocol = RemoveScheme(address.Protocol);
            return address.WithProtocol(protocol);
        }
    }

    /// <summary>
    /// An adapter that wraps a transport and provides interception capabilities
    /// </summary>
    public abstract class AbstractTransportAdapter : Transport
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="wrappedTransport">TBD</param>
        protected AbstractTransportAdapter(Transport wrappedTransport)
        {
            WrappedTransport = wrappedTransport;
        }

        /// <summary>
        /// TBD
        /// </summary>
        protected Transport WrappedTransport;

        /// <summary>
        /// TBD
        /// </summary>
        protected abstract SchemeAugmenter SchemeAugmenter { get; }

        /// <summary>
        /// TBD
        /// </summary>
        public override string SchemeIdentifier
        {
            get
            {
                return SchemeAugmenter.AugmentScheme(WrappedTransport.SchemeIdentifier);
            }
        }

        /// <summary>
        /// TBD
        /// </summary>
        public override long MaximumPayloadBytes
        {
            get
            {
                return WrappedTransport.MaximumPayloadBytes;
            }
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="listenAddress">TBD</param>
        /// <param name="listenerTask">TBD</param>
        /// <returns>TBD</returns>
        protected abstract Task<IAssociationEventListener> InterceptListen(Address listenAddress,
            Task<IAssociationEventListener> listenerTask);

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="remoteAddress">TBD</param>
        /// <param name="statusPromise">TBD</param>
        protected abstract void InterceptAssociate(Address remoteAddress,
            TaskCompletionSource<AssociationHandle> statusPromise);

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="remote">TBD</param>
        /// <returns>TBD</returns>
        public override bool IsResponsibleFor(Address remote)
        {
            return WrappedTransport.IsResponsibleFor(remote);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <returns>TBD</returns>
        public override Task<Tuple<Address, TaskCompletionSource<IAssociationEventListener>>> Listen()
        {
            var upstreamListenerPromise = new TaskCompletionSource<IAssociationEventListener>();
            return WrappedTransport.Listen().ContinueWith(async listenerTask =>
            {
                var listenAddress = listenerTask.Result.Item1;
                var listenerPromise = listenerTask.Result.Item2;
                listenerPromise.TrySetResult(await InterceptListen(listenAddress, upstreamListenerPromise.Task).ConfigureAwait(false));
                return
                    new Tuple<Address, TaskCompletionSource<IAssociationEventListener>>(
                        SchemeAugmenter.AugmentScheme(listenAddress), upstreamListenerPromise);
            }, TaskContinuationOptions.ExecuteSynchronously).Unwrap();
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="remoteAddress">TBD</param>
        /// <returns>TBD</returns>
        public override Task<AssociationHandle> Associate(Address remoteAddress)
        {
            var statusPromise = new TaskCompletionSource<AssociationHandle>();
            InterceptAssociate(SchemeAugmenter.RemoveScheme(remoteAddress), statusPromise);
            return statusPromise.Task;
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <returns>TBD</returns>
        public override Task<bool> Shutdown()
        {
            return WrappedTransport.Shutdown();
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    internal abstract class AbstractTransportAdapterHandle : AssociationHandle
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="wrappedHandle">TBD</param>
        /// <param name="addedSchemeIdentifier">TBD</param>
        protected AbstractTransportAdapterHandle(AssociationHandle wrappedHandle, string addedSchemeIdentifier)
            : this(wrappedHandle.LocalAddress, wrappedHandle.RemoteAddress, wrappedHandle, addedSchemeIdentifier) { }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="originalLocalAddress">TBD</param>
        /// <param name="originalRemoteAddress">TBD</param>
        /// <param name="wrappedHandle">TBD</param>
        /// <param name="addedSchemeIdentifier">TBD</param>
        protected AbstractTransportAdapterHandle(Address originalLocalAddress, Address originalRemoteAddress, AssociationHandle wrappedHandle, string addedSchemeIdentifier) : base(originalLocalAddress, originalRemoteAddress)
        {
            WrappedHandle = wrappedHandle;
            OriginalRemoteAddress = originalRemoteAddress;
            OriginalLocalAddress = originalLocalAddress;
            SchemeAugmenter = new SchemeAugmenter(addedSchemeIdentifier);
            RemoteAddress = SchemeAugmenter.AugmentScheme(OriginalRemoteAddress);
            LocalAddress = SchemeAugmenter.AugmentScheme(OriginalLocalAddress);
        }

        /// <summary>
        /// TBD
        /// </summary>
        public Address OriginalLocalAddress { get; private set; }

        /// <summary>
        /// TBD
        /// </summary>
        public Address OriginalRemoteAddress { get; private set; }

        /// <summary>
        /// TBD
        /// </summary>
        public AssociationHandle WrappedHandle { get; private set; }

        /// <summary>
        /// TBD
        /// </summary>
        protected SchemeAugmenter SchemeAugmenter { get; private set; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="other">TBD</param>
        /// <returns>TBD</returns>
        protected bool Equals(AbstractTransportAdapterHandle other)
        {
            return Equals(OriginalLocalAddress, other.OriginalLocalAddress) && Equals(OriginalRemoteAddress, other.OriginalRemoteAddress) && Equals(WrappedHandle, other.WrappedHandle);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="obj">TBD</param>
        /// <returns>TBD</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AbstractTransportAdapterHandle) obj);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <returns>TBD</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = base.GetHashCode() + (OriginalLocalAddress != null ? OriginalLocalAddress.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OriginalRemoteAddress != null ? OriginalRemoteAddress.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (WrappedHandle != null ? WrappedHandle.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    /// <summary>
    /// Marker interface for all transport operations
    /// </summary>
    internal abstract class TransportOperation : INoSerializationVerificationNeeded
    {
        /// <summary>
        /// TBD
        /// </summary>
        public static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// TBD
    /// </summary>
    internal sealed class ListenerRegistered : TransportOperation
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="listener">TBD</param>
        public ListenerRegistered(IAssociationEventListener listener)
        {
            Listener = listener;
        }

        /// <summary>
        /// TBD
        /// </summary>
        public IAssociationEventListener Listener { get; private set; }
    }

    /// <summary>
    /// TBD
    /// </summary>
    internal sealed class AssociateUnderlying : TransportOperation
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="remoteAddress">TBD</param>
        /// <param name="statusPromise">TBD</param>
        public AssociateUnderlying(Address remoteAddress, TaskCompletionSource<AssociationHandle> statusPromise)
        {
            RemoteAddress = remoteAddress;
            StatusPromise = statusPromise;
        }

        /// <summary>
        /// TBD
        /// </summary>
        public Address RemoteAddress { get; private set; }

        /// <summary>
        /// TBD
        /// </summary>
        public TaskCompletionSource<AssociationHandle> StatusPromise { get; private set; }
    }

    /// <summary>
    /// TBD
    /// </summary>
    internal sealed class ListenUnderlying : TransportOperation
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="listenAddress">TBD</param>
        /// <param name="upstreamListener">TBD</param>
        public ListenUnderlying(Address listenAddress, Task<IAssociationEventListener> upstreamListener)
        {
            UpstreamListener = upstreamListener;
            ListenAddress = listenAddress;
        }

        /// <summary>
        /// TBD
        /// </summary>
        public Address ListenAddress { get; private set; }

        /// <summary>
        /// TBD
        /// </summary>
        public Task<IAssociationEventListener> UpstreamListener { get; private set; }
    }

    /// <summary>
    /// TBD
    /// </summary>
    internal sealed class DisassociateUnderlying : TransportOperation, IDeadLetterSuppression
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="info">TBD</param>
        public DisassociateUnderlying(DisassociateInfo info = DisassociateInfo.Unknown)
        {
            Info = info;
        }

        /// <summary>
        /// TBD
        /// </summary>
        public DisassociateInfo Info { get; private set; }
    }

    /// <summary>
    /// TBD
    /// </summary>
    public abstract class ActorTransportAdapter : AbstractTransportAdapter
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="wrappedTransport">TBD</param>
        /// <param name="system">TBD</param>
        protected ActorTransportAdapter(Transport wrappedTransport, ActorSystem system) : base(wrappedTransport)
        {
            System = system;
        }

        /// <summary>
        /// TBD
        /// </summary>
        protected abstract string ManagerName { get; }
        /// <summary>
        /// TBD
        /// </summary>
        protected abstract Props ManagerProps { get; }


        /// <summary>
        /// TBD
        /// </summary>
        public static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// TBD
        /// </summary>
        protected volatile IActorRef manager;

        private Task<IActorRef> RegisterManager()
        {
            return System.ActorSelection("/system/transports").Ask<IActorRef>(new RegisterTransportActor(ManagerProps, ManagerName));
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="listenAddress">TBD</param>
        /// <param name="listenerTask">TBD</param>
        /// <returns>TBD</returns>
        protected override Task<IAssociationEventListener> InterceptListen(Address listenAddress, Task<IAssociationEventListener> listenerTask)
        {
            return RegisterManager().ContinueWith(mgrTask =>
            {
                manager = mgrTask.Result;
                manager.Tell(new ListenUnderlying(listenAddress, listenerTask));
                return (IAssociationEventListener)new ActorAssociationEventListener(manager);
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="remoteAddress">TBD</param>
        /// <param name="statusPromise">TBD</param>
        protected override void InterceptAssociate(Address remoteAddress, TaskCompletionSource<AssociationHandle> statusPromise)
        {
            manager.Tell(new AssociateUnderlying(remoteAddress, statusPromise));
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <returns>TBD</returns>
        public override Task<bool> Shutdown()
        {
            var stopTask = manager.GracefulStop((RARP.For(System).Provider).RemoteSettings.FlushWait);
            var transportStopTask = WrappedTransport.Shutdown();
            return Task.WhenAll(stopTask, transportStopTask).ContinueWith(x => x.IsCompleted && !(x.IsFaulted || x.IsCanceled), TaskContinuationOptions.ExecuteSynchronously);
        }
    }

    /// <summary>
    /// TBD
    /// </summary>
    internal abstract class ActorTransportAdapterManager : UntypedActor
    {
        /// <summary>
        /// Lightweight Stash implementation
        /// </summary>
        protected Queue<object> DelayedEvents = new Queue<object>();

        /// <summary>
        /// TBD
        /// </summary>
        protected IAssociationEventListener AssociationListener;
        /// <summary>
        /// TBD
        /// </summary>
        protected Address LocalAddress;
        /// <summary>
        /// TBD
        /// </summary>
        protected long UniqueId = 0L;

        /// <summary>
        /// TBD
        /// </summary>
        /// <returns>TBD</returns>
        protected long NextId()
        {
            return Interlocked.Increment(ref UniqueId);
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="message">TBD</param>
        protected override void OnReceive(object message)
        {
            PatternMatch.Match(message)
                .With<ListenUnderlying>(listen =>
                {
                    LocalAddress = listen.ListenAddress;
                    var capturedSelf = Self;
                    listen.UpstreamListener.ContinueWith(
                        listenerRegistered => capturedSelf.Tell(new ListenerRegistered(listenerRegistered.Result)),
                        TaskContinuationOptions.ExecuteSynchronously);
                })
                .With<ListenerRegistered>(listener =>
                {
                    AssociationListener = listener.Listener;
                    foreach (var dEvent in DelayedEvents)
                    {
                        Self.Tell(dEvent, ActorRefs.NoSender);
                    }
                    DelayedEvents = new Queue<object>();
                    Context.Become(Ready);
                })
                .Default(m => DelayedEvents.Enqueue(m));
        }

        /// <summary>
        /// Method to be implemented for child classes - processes messages once the transport is ready to send / receive
        /// </summary>
        /// <param name="message">TBD</param>
        protected abstract void Ready(object message);
    }
}

