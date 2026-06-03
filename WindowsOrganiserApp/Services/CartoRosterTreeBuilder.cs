using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Services;

public static class CartoRosterTreeBuilder
{
    public static void Rebuild(
        CartoViewModel vm,
        IList<CartoRosterTreeNode> roots,
        Func<string, bool, bool>? isExpanded = null)
    {
        roots.Clear();
        vm.EnsureAccountsAssignedToDefaultUser();

        var localChars = vm.Characters
            .Where(vm.IsCharacterEligibleForRosterTree)
            .ToList();

        if (localChars.Count == 0)
            return;

        var assignedCharIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in vm.GetOrderedUsers())
        {
            var userAccounts = vm.Accounts
                .Where(a => vm.GetUserIdForAccount(a) == user.Id)
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (userAccounts.Count == 0)
                continue;

            var userNode = CreateUserNode(vm, user, isExpanded);
            foreach (var account in userAccounts)
            {
                var accountNode = CreateAccountNode(vm, user, account, isExpanded);
                var accountChars = localChars
                    .Where(c => string.Equals(c.AccountId, account.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var c in accountChars)
                    assignedCharIds.Add(c.Id);

                foreach (var category in CartoViewModel.RosterCategoryStatuses)
                {
                    var statuses = CartoViewModel.StatusesForRosterCategory(category).ToHashSet();
                    var inCategory = accountChars
                        .Where(c => statuses.Contains(c.Status))
                        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    accountNode.Children.Add(CreateCategoryNode(vm, user, account, category, inCategory, isExpanded));
                }

                AddChild(userNode, accountNode);
            }

            if (userNode.Children.Count > 0)
                roots.Add(userNode);
        }

        AppendOrphanCharacters(vm, roots, localChars, assignedCharIds, isExpanded);
    }

    private static void AddChild(CartoRosterTreeNode parent, CartoRosterTreeNode child)
    {
        child.Depth = parent.Depth + 1;
        parent.Children.Add(child);
    }

    private static void AppendOrphanCharacters(
        CartoViewModel vm,
        IList<CartoRosterTreeNode> roots,
        List<WowCharacter> localChars,
        HashSet<string> assignedCharIds,
        Func<string, bool, bool>? isExpanded)
    {
        var orphans = localChars.Where(c => !assignedCharIds.Contains(c.Id)).ToList();
        if (orphans.Count == 0)
            return;

        var moi = vm.GetOrderedUsers()
            .FirstOrDefault(CartoViewModel.IsDefaultCartoUser);
        if (moi == null)
            return;

        var userNode = roots.FirstOrDefault(n => n.User?.Id == moi.Id);
        if (userNode == null)
        {
            userNode = CreateUserNode(vm, moi, isExpanded);
            roots.Insert(0, userNode);
        }

        var orphanAccount = new WowAccount
        {
            Id = "__orphan__",
            Name = "Comptes non liés",
            SourceFolder = ""
        };
        var accountNode = CreateAccountNode(vm, moi, orphanAccount, isExpanded);
        foreach (var category in CartoViewModel.RosterCategoryStatuses)
        {
            var statuses = CartoViewModel.StatusesForRosterCategory(category).ToHashSet();
            var inCategory = orphans
                .Where(c => statuses.Contains(c.Status))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (inCategory.Count == 0)
                continue;

            accountNode.Children.Add(CreateCategoryNode(vm, moi, orphanAccount, category, inCategory, isExpanded));
        }

        if (accountNode.Children.Count > 0)
            AddChild(userNode, accountNode);
    }

    private static CartoRosterTreeNode CreateUserNode(
        CartoViewModel vm,
        CartoUser user,
        Func<string, bool, bool>? isExpanded)
    {
        var key = RosterExpandKeys.User(user.Id);
        return new()
        {
            Kind = CartoRosterNodeKind.User,
            User = user,
            Title = user.Name,
            GoldCopper = vm.GetUserTotalGoldCopper(user.Id),
            ExpandKey = key,
            IsExpanded = isExpanded?.Invoke(key, true) ?? true,
            Depth = 0
        };
    }

    private static CartoRosterTreeNode CreateAccountNode(
        CartoViewModel vm,
        CartoUser user,
        WowAccount account,
        Func<string, bool, bool>? isExpanded)
    {
        var key = RosterExpandKeys.Account(user.Id, account.Id);
        return new()
        {
            Kind = CartoRosterNodeKind.Account,
            User = user,
            Account = account,
            Title = account.Name,
            GoldCopper = vm.GetAccountGoldCopper(account.SourceFolder),
            ExpandKey = key,
            IsExpanded = isExpanded?.Invoke(key, true) ?? true
        };
    }

    private static CartoRosterTreeNode CreateCategoryNode(
        CartoViewModel vm,
        CartoUser user,
        WowAccount account,
        CharacterStatus category,
        IReadOnlyList<WowCharacter> characters,
        Func<string, bool, bool>? isExpanded)
    {
        var key = RosterExpandKeys.Category(user.Id, account.Id, category);
        var defaultExpanded = category == CharacterStatus.Main && characters.Count > 0;
        var node = new CartoRosterTreeNode
        {
            Kind = CartoRosterNodeKind.Category,
            User = user,
            Account = account,
            Category = category,
            Title = CartoViewModel.RosterCategoryTitle(category),
            GoldCopper = vm.GetCategoryGoldCopper(
                vm.GetLocalCharactersForUserCategory(user.Id, category)),
            CharacterCount = vm.CountLocalCharactersInCategory(user.Id, category),
            ExpandKey = key,
            IsExpanded = isExpanded?.Invoke(key, defaultExpanded) ?? defaultExpanded
        };

        node.CategoryCharacters.AddRange(characters);
        return node;
    }
}
