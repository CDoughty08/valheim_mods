namespace VariaChestFocus
{
    internal static class ContainerAccess
    {
        // Only UI actions on this client's current container may bypass the in-use check.
        internal static bool CanModify(Player player, Container container, bool allowInUse = false)
        {
            return player != null && container != null
                && container.m_rootObjectOverride == null && container.GetComponent<TombStone>() == null
                && !container.m_loading && (allowInUse || !container.IsInUse())
                && container.m_nview != null && container.m_nview.IsValid() && container.m_nview.IsOwner()
                && container.CheckAccess(player.GetPlayerID())
                && (!container.m_checkGuardStone
                    || PrivateArea.CheckAccess(container.transform.position, 0f, flash: false));
        }
    }
}
