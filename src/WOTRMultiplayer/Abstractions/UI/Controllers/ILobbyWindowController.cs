using System;
using System.Collections.Generic;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Connectivity;
using WOTRMultiplayer.UI.Windows;

namespace WOTRMultiplayer.Abstractions.UI.Controllers
{
    public interface ILobbyWindowController
    {
        void UpdatePlayers(List<NetworkPlayer> players, bool isDropdownInteractable);

        void InitializeContent(LobbyWindowOwner owner, Transform parent);

        void ResetData();

        void UpdateServerInfo(GameConnectivity connectivity);

        void UpdateCharacters(List<NetworkCharacter> characters, bool isDropdownInteractable);

        void UpdateCharacterOwnerDropdown(NetworkCharacter character, bool silent = false);

        void UpdateAdvancedControls(IDictionary<NetworkPlayerControlledFeature, long> features, bool silent = false);

        void SetActiveOwner(LobbyWindowOwner owner);

        void ResetOwnerContent(LobbyWindowOwner owner);

        void CloseWindow();

        void EnsureStandaloneWindowInitialized();

        void Reset();

        void UpdateLoadingProgress(Dictionary<long, float> progress);

        public ILobbyWindow Window { get; }

        Action<NetworkCharacter, NetworkPlayer> OnCharacterOwnerChanged { get; set; }

        Action<NetworkPlayerControlledFeature, NetworkPlayer> OnFeatureControlChanged { get; set; }
    }
}
