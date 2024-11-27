using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.Core.AudioSystem
{
    internal sealed class LoopingSfxRegistry
    {
        private readonly Dictionary<int, LoopingSfxChannel> _channels = new();
        private readonly Transform _parent;
        private readonly AudioMixerGroup _outputGroup;
        private int _nextSlotId;

        public LoopingSfxRegistry(Transform parent, AudioMixerGroup outputGroup)
        {
            _parent = parent;
            _outputGroup = outputGroup;
        }

        public LoopingSfxHandle Allocate()
        {
            int slotId = ++_nextSlotId;
            var channel = new LoopingSfxChannel(_parent, _outputGroup, slotId);
            _channels.Add(slotId, channel);
            return new LoopingSfxHandle(slotId);
        }

        public bool TryGet(LoopingSfxHandle handle, out LoopingSfxChannel channel)
        {
            channel = null;

            if (!handle.IsValid)
                return false;

            return _channels.TryGetValue(handle.Id, out channel);
        }

        public IEnumerable<LoopingSfxChannel> AllChannels =>
            _channels.Values;

        public void Remove(LoopingSfxHandle handle)
        {
            if (!handle.IsValid)
                return;

            if (_channels.TryGetValue(handle.Id, out LoopingSfxChannel channel))
                RemoveChannel(handle.Id, channel);
        }

        public void DisposeAll()
        {
            if (_channels.Count == 0)
                return;

            var ids = new List<int>(_channels.Keys);

            for (int i = 0; i < ids.Count; i++)
            {
                if (_channels.TryGetValue(ids[i], out LoopingSfxChannel channel))
                    RemoveChannel(ids[i], channel);
            }
        }

        private void RemoveChannel(int slotId, LoopingSfxChannel channel)
        {
            _channels.Remove(slotId);
            channel.DisposeSource();
        }
    }
}
