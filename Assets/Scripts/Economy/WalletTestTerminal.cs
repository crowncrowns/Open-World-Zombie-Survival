using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Economy
{
    public enum WalletTestAction { Credit = 0, Debit = 1, PlayerPaysBudget = 2, BudgetPaysPlayer = 3, ToggleBudgetMode = 4 }
    public sealed class WalletTestTerminal : Interactable
    {
        [SerializeField, Tooltip("Явная тестовая денежная операция на одно нажатие E; не торговля и не награда.")]
        private WalletTestAction action;
        [SerializeField, Min(1), Tooltip("Сумма операции, целые рубли. Не влияет на настройки рецептов ремонта.")]
        private long amount = 100;
        [SerializeField, Tooltip("Общий тестовый бюджет для платежей/смены режима. У каждого будущего NPC свой Wallet.")]
        private TraderBudget budget;
        public override string GetPrompt(PlayerInteractor actor)
        {
            switch (action)
            {
                case WalletTestAction.Credit: return "Тест: начислить " + Wallet.FormatRubles(amount);
                case WalletTestAction.Debit: return "Тест: списать " + Wallet.FormatRubles(amount);
                case WalletTestAction.PlayerPaysBudget: return "Тест: заплатить бюджету " + Wallet.FormatRubles(amount);
                case WalletTestAction.BudgetPaysPlayer: return "Тест: получить из бюджета " + Wallet.FormatRubles(amount) + " [" + (budget != null ? budget.Funds.FormattedBalance : "нет бюджета") + "]";
                default: return "Тест: бюджет → " + (budget != null && budget.IsInfinite ? "конечный" : "бесконечный");
            }
        }
        protected override bool PerformInteraction(PlayerInteractor actor)
        {
            var wallet = actor.GetComponent<Wallet>();
            if (wallet == null) { actor.ShowFeedback("У актёра нет Wallet"); return false; }
            bool result;
            switch (action)
            {
                case WalletTestAction.Credit: result = wallet.TryCredit(amount); break;
                case WalletTestAction.Debit: result = wallet.TryDebit(amount); break;
                case WalletTestAction.PlayerPaysBudget: result = budget != null && budget.TryReceiveFrom(wallet, amount); break;
                case WalletTestAction.BudgetPaysPlayer: result = budget != null && budget.TryPay(wallet, amount); break;
                default:
                    if (budget == null) return false;
                    budget.SetInfinite(!budget.IsInfinite); actor.ShowFeedback("Бюджет: " + budget.Funds.FormattedBalance + "; конечный резерв " + Wallet.FormatRubles(budget.Funds.Balance)); return true;
            }
            actor.ShowFeedback(result ? "Баланс: " + wallet.FormattedBalance : "Отказ: недостаточно рублей, переполнение или недоступный кошелёк");
            return result;
        }
    }
}
