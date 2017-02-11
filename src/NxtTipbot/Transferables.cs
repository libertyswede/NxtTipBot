using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace NxtTipbot
{
    public interface ITransferables
    {
        ReadOnlyCollection<NxtTransferable> NxtTransferables { get; }
        void AddTransferable(NxtTransferable transferable);
        NxtTransferable GetTransferable(string name);
        bool ContainsTransferable(NxtTransferable transferable);
        NxtTransferable GetTransferableByReactionId(string reactionId);
    }

    public class Transferables : ITransferables
    {
        public ReadOnlyCollection<NxtTransferable> NxtTransferables { get; private set; }
        private readonly List<NxtTransferable> transferables = new List<NxtTransferable> { Nxt.Singleton };
        private readonly Dictionary<string, NxtTransferable> transferableNames = new Dictionary<string, NxtTransferable> { { Nxt.Singleton.Name.ToLowerInvariant(), Nxt.Singleton } };
        private readonly Dictionary<ulong, NxtTransferable> transferableIds = new Dictionary<ulong, NxtTransferable>();

        public void AddTransferable(NxtTransferable transferable)
        {
            string name = transferable.Name.ToLowerInvariant();
            List<string> transferableMonikerNames = transferable.Monikers.Select(m => m.ToLowerInvariant()).ToList(); ;

            VerifyTransferableNames(transferable, name, transferableMonikerNames);

            foreach (var moniker in transferableMonikerNames)
            {
                transferableNames.Add(moniker, transferable);
            }
            transferableNames.Add(name, transferable);
            transferableIds.Add(transferable.Id, transferable);
            transferables.Add(transferable);
            NxtTransferables = transferables.AsReadOnly();
        }

        private void VerifyTransferableNames(NxtTransferable transferable, string name, List<string> transferableMonikerNames)
        {
            if (transferableIds.ContainsKey(transferable.Id))
            {
                throw new ArgumentException(nameof(transferable), $"Transferable Id must be unique, {transferable.Id} was already added.");
            }
            if (transferableNames.ContainsKey(name))
            {
                throw new ArgumentException(nameof(transferable), $"Name of transferable must be unique, {transferable.Name} was already added.");
            }
            foreach (var moniker in transferableMonikerNames)
            {
                if (transferableNames.ContainsKey(moniker))
                {
                    throw new ArgumentException(nameof(transferable), $"Name of transferable must be unique, moniker {moniker} for {transferable.Name} was already added.");
                }
            }
        }

        public bool ContainsTransferable(NxtTransferable transferable)
        {
            return transferables.Contains(transferable);
        }

        public NxtTransferable GetTransferable(string name)
        {
            NxtTransferable transferable = null;

            if (!transferableNames.TryGetValue(name.ToLowerInvariant(), out transferable) && name.IsNumeric())
            {
                var id = ulong.Parse(name);
                transferableIds.TryGetValue(id, out transferable);
            }

            return transferable;
        }

        public NxtTransferable GetTransferableByReactionId(string reactionId)
        {
            var transferable = transferables.FirstOrDefault(t => t.Reactions.Any(r => r.ReactionId == reactionId));
            return transferable;
        }
    }
}
