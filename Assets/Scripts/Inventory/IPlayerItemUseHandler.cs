namespace ZombieSurvival.Inventory
{
    /// <summary>Контракт дополнительного компонента инструмента/способности. Хотбар проверяет владение и ввод.
    /// Изменения предмета фиксируются через ItemStorage; ItemStack.Copy() нельзя вставлять обратно в хранилище.</summary>
    public interface IPlayerItemUseHandler
    {
        bool CanHandle(ItemStack item);
        bool TryUsePrimary(PlayerHotbar hotbar, ItemStack item);
        bool TryUseSecondary(PlayerHotbar hotbar, ItemStack item);
    }
}
