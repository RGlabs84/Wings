namespace WingsoftheValkyrie
{
    /// <summary>
    /// Device-agnostic reads of the player's Jump binding.
    ///
    /// Valheim keeps keyboard/mouse and gamepad on *separate* virtual buttons: "Jump" is only ever
    /// the keyboard binding and "JoyJump" only ever the gamepad one, so anything that reads a single
    /// name silently works on one device and not the other. Vanilla's own jump
    /// (PlayerController.FixedUpdate) ORs the two together, and this mirrors that.
    /// </summary>
    internal static class ValkyrieInput
    {
        /// <summary>
        /// True for the one frame the player presses Jump on any input device.
        /// </summary>
        public static bool JumpPressed(Player player)
        {
            if (!AcceptsInput(player)) return false;

            if (ZInput.GetButtonDown("Jump")) return true;

            // The gamepad half needs two extra gates that the keyboard half does not: the same face
            // button confirms selections in the radial and build menus, so vanilla suppresses
            // JoyJump while either is up. Without this, opening the radial mid-glide to swap items
            // would spend stamina on a flap.
            return ZInput.GetButtonDown("JoyJump")
                   && !Hud.InRadial()
                   && !Hud.IsPieceSelectionVisible();
        }

        /// <summary>
        /// Mirrors Player.TakeInput(), which is protected and so cannot be called directly. The mod
        /// must not act on a press the game itself is swallowing -- otherwise typing a space in chat
        /// or hitting the gamepad's confirm button in the inventory would flap while airborne.
        /// </summary>
        private static bool AcceptsInput(Player player)
        {
            if (player == null) return false;
            if (player.IsDead() || player.InCutscene() || player.IsTeleporting()) return false;

            // "Console" here is Valheim's in-game console, not System.Console. This file
            // deliberately has no `using System;` so the unqualified name cannot bind to the wrong
            // type, but qualify it anyway so that stays true if someone adds one later.
            if (global::Console.IsVisible()) return false;

            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            if (TextInput.IsVisible()) return false;
            if (Menu.IsVisible()) return false;
            if (InventoryGui.IsVisible()) return false;
            if (StoreGui.IsVisible()) return false;
            if (Minimap.IsOpen()) return false;

            return true;
        }
    }
}
