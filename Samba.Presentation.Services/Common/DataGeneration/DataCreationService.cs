using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Entities;
using Samba.Domain.Models.Inventory;
using Samba.Domain.Models.Menus;
using Samba.Domain.Models.Settings;
using Samba.Domain.Models.Tickets;
using Samba.Domain.Models.Users;
using Samba.Infrastructure.Data;
using Samba.Infrastructure.Settings;
using Samba.Localization.Properties;
using Samba.Persistance.Data;
using Samba.Presentation.Services.Common.DataGeneration.Templates;

namespace Samba.Presentation.Services.Common.DataGeneration
{
    public class DataCreationService
    {
        private readonly IWorkspace _workspace;

        public DataCreationService()
        {
            _workspace = WorkspaceFactory.Create();
        }

        private bool ShouldCreateData()
        {
            if (RuleGenerator.ShouldRegenerateRules())
            {
                new RuleGenerator().RegenerateRules(_workspace);
            }
            return _workspace.Count<User>() == 0;
        }

        public void CreateData()
        {
            CreateDefaultCurrenciesIfNeeded();

            if (!ShouldCreateData()) return;


            IDataTemplate template;

            if(string.IsNullOrWhiteSpace(LocalSettings.TemplateName) || LocalSettings.TemplateName.ToLower().Trim() == "default")
            {
                template = new DefaultTemplate();
                template.Create(this, _workspace);
            }
            else if(LocalSettings.TemplateName.ToLower().Trim() == "liquorstore")
            {
                template = new LiquorStoreTemplate();
                template.Create(this, _workspace);
            }
            

            ImportItems(BatchCreateEntities);
            ImportItems(BatchCreateTransactionTypes);
            ImportItems(BatchCreateTransactionTypeDocuments);

            _workspace.CommitChanges();
            _workspace.Dispose();



        }

        private void ImportItems<T>(Func<string[], IWorkspace, IEnumerable<T>> func) where T : class
        {
            var fileName = string.Format("{0}\\Imports\\" + typeof(T).Name.ToLower() + "{1}.txt", LocalSettings.AppPath, "_" + LocalSettings.CurrentLanguage);
            if (!File.Exists(fileName))
                fileName = string.Format("{0}\\Imports\\" + typeof(T).Name.ToLower() + ".txt", LocalSettings.AppPath);
            if (!File.Exists(fileName)) return;
            var lines = File.ReadAllLines(fileName);
            var items = func(lines, _workspace);
            items.ToList().ForEach(x => _workspace.Add(x));
            _workspace.CommitChanges();
        }

        internal void ImportTableResources(EntityType tableTemplate, TicketType ticketType)
        {
            var fileName = string.Format("{0}/Imports/table{1}.txt", LocalSettings.AppPath, "_" + LocalSettings.CurrentLanguage);

            if (!File.Exists(fileName))
                fileName = string.Format("{0}/Imports/table.txt", LocalSettings.AppPath);

            if (!File.Exists(fileName)) return;

            var lines = File.ReadAllLines(fileName);
            var items = BatchCreateEntitiesWithTemplate(lines, _workspace, tableTemplate).ToList();
            items.ForEach(_workspace.Add);

            _workspace.CommitChanges();

            var screen = new EntityScreen { Name = Resources.All_Tables, DisplayState = "Status", TicketTypeId = ticketType.Id, ColumnCount = 7, EntityTypeId = tableTemplate.Id, FontSize = 50 };
            screen.EntityScreenMaps.Add(new EntityScreenMap());
            _workspace.Add(screen);

            foreach (var resource in items)
            {
                resource.EntityTypeId = tableTemplate.Id;
                screen.AddScreenItem(new EntityScreenItem(tableTemplate,resource));
                var state = new EntityStateValue { EntityId = resource.Id };
                state.SetStateValue("Status", Resources.Available, "");
                _workspace.Add(state);
            }

            _workspace.CommitChanges();
        }

        internal void ImportMenus(ScreenMenu screenMenu)
        {
            var fileName = string.Format("{0}/Imports/menu{1}.txt", LocalSettings.AppPath, "_" + LocalSettings.CurrentLanguage);

            if (!File.Exists(fileName))
                fileName = string.Format("{0}/Imports/menu.txt", LocalSettings.AppPath);

            if (!File.Exists(fileName)) return;

            var lines = File.ReadAllLines(fileName, Encoding.UTF8);

            var items = BatchCreateMenuItems(lines, _workspace).ToList();
            items.ForEach(_workspace.Add);
            _workspace.CommitChanges();
            var groupCodes = items.Select(x => x.GroupCode).Distinct().Where(x => !string.IsNullOrEmpty(x));

            foreach (var groupCode in groupCodes)
            {
                var code = groupCode;
                screenMenu.AddCategory(code);
                screenMenu.AddItemsToCategory(groupCode, items.Where(x => x.GroupCode == code).ToList());
            }
        }

        public IEnumerable<Entity> BatchCreateEntitiesWithTemplate(string[] values, IWorkspace workspace, EntityType template)
        {
            IList<Entity> result = new List<Entity>();
            if (values.Length > 0)
            {
                foreach (var entity in from value in values
                                       where !value.StartsWith("#")
                                       let entityName = value
                                       let count = Dao.Count<Entity>(y => y.Name == entityName.Trim())
                                       where count == 0
                                       select new Entity { Name = value.Trim(), EntityTypeId = template.Id }
                                           into resource
                                           where result.Count(x => x.Name.ToLower() == resource.Name.ToLower()) == 0
                                           select resource)
                {
                    result.Add(entity);
                }
            }
            return result;
        }

        public IEnumerable<MenuItem> BatchCreateMenuItems(string[] values, IWorkspace workspace)
        {
            var ds = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            IList<MenuItem> result = new List<MenuItem>();
            if (values.Length > 0)
            {
                var currentCategory = Resources.Common;

                foreach (var item in values)
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;

                    if (item.StartsWith("#"))
                    {
                        currentCategory = item.Trim('#', ' ');
                    }
                    else if (item.Contains(" "))
                    {
                        IList<string> parts = new List<string>(item.Split(' '));
                        var price = ConvertToDecimal(parts[parts.Count - 1], ds);
                        parts.RemoveAt(parts.Count - 1);

                        var itemName = string.Join(" ", parts.ToArray());
                        var mi = MenuItem.Create();
                        mi.Name = itemName;
                        mi.Portions[0].Price = price;
                        mi.GroupCode = currentCategory;
                        result.Add(mi);
                    }
                }
            }
            return result;
        }

        public IEnumerable<Account> BatchCreateAccounts(string[] values, IWorkspace workspace)
        {
            IList<Account> result = new List<Account>();
            if (values.Length > 0)
            {
                var templates = workspace.All<AccountType>().ToList();
                AccountType currentTemplate = null;

                foreach (var item in values)
                {
                    if (item.StartsWith("#"))
                    {
                        var templateName = item.Trim('#', ' ');
                        currentTemplate = templates.SingleOrDefault(x => x.Name.ToLower() == templateName.ToLower());
                        if (currentTemplate == null)
                        {
                            using (var w = WorkspaceFactory.Create())
                            {
                                currentTemplate = new AccountType { Name = templateName };
                                w.Add(currentTemplate);
                                w.CommitChanges();
                            }
                        }
                    }
                    else if (currentTemplate != null)
                    {
                        var accountName = item.ToLower().Trim();
                        if (workspace.Single<Account>(x => x.Name.ToLower() == accountName) == null)
                        {
                            var account = new Account { Name = item, AccountTypeId = currentTemplate.Id };
                            result.Add(account);
                        }
                    }
                }
            }
            return result;
        }

        public IEnumerable<Entity> BatchCreateEntities(string[] values, IWorkspace workspace)
        {
            return EntityCreator.ImportText(values, workspace);
        }

        public IEnumerable<AccountTransactionType> BatchCreateTransactionTypes(string[] values, IWorkspace workspace)
        {
            IList<AccountTransactionType> result = new List<AccountTransactionType>();
            if (values.Length > 0)
            {
                foreach (var item in values)
                {
                    var parts = item.Split(';');
                    if (parts.Count() > 2)
                    {
                        var name = parts[0].Trim();

                        if (workspace.Single<AccountTransactionType>(x => x.Name.ToLower() == name.ToLower()) != null) continue;

                        var sTempName = parts[1].Trim();
                        var tTempName = parts[2].Trim();
                        var dsa = parts.Length > 2 ? parts[3].Trim() : "";
                        var dta = parts.Length > 3 ? parts[4].Trim() : "";

                        var sAccTemplate = workspace.Single<AccountType>(x => x.Name.ToLower() == sTempName.ToLower());
                        if (sAccTemplate == null)
                        {
                            using (var w = WorkspaceFactory.Create())
                            {
                                sAccTemplate = new AccountType { Name = sTempName };
                                w.Add(sAccTemplate);
                                w.CommitChanges();
                            }
                        }

                        var tAccTemplate = workspace.Single<AccountType>(x => x.Name.ToLower() == tTempName.ToLower());
                        if (tAccTemplate == null)
                        {
                            using (var w = WorkspaceFactory.Create())
                            {
                                tAccTemplate = new AccountType { Name = tTempName };
                                w.Add(tAccTemplate);
                                w.CommitChanges();
                            }
                        }

                        var sa = !string.IsNullOrEmpty(dsa)
                            ? workspace.Single<Account>(x => x.Name.ToLower() == dsa.ToLower())
                            : null;

                        if (!string.IsNullOrEmpty(dsa) && sa == null)
                        {
                            using (var w = WorkspaceFactory.Create())
                            {
                                sa = new Account { Name = dsa, AccountTypeId = sAccTemplate.Id };
                                w.Add(sa);
                                w.CommitChanges();
                            }
                        }

                        var ta = !string.IsNullOrEmpty(dta)
                            ? workspace.Single<Account>(x => x.Name.ToLower() == dta.ToLower())
                            : null;

                        if (!string.IsNullOrEmpty(dta) && ta == null)
                        {
                            using (var w = WorkspaceFactory.Create())
                            {
                                ta = new Account { Name = dta, AccountTypeId = tAccTemplate.Id };
                                w.Add(ta);
                                w.CommitChanges();
                            }
                        }

                        var resultItem = new AccountTransactionType
                                             {
                                                 Name = name,
                                                 SourceAccountTypeId = sAccTemplate.Id,
                                                 TargetAccountTypeId = tAccTemplate.Id
                                             };

                        if (sa != null) resultItem.DefaultSourceAccountId = sa.Id;
                        if (ta != null) resultItem.DefaultTargetAccountId = ta.Id;

                        result.Add(resultItem);
                    }
                }
            }
            return result;
        }

        public IEnumerable<AccountTransactionDocumentType> BatchCreateTransactionTypeDocuments(string[] values, IWorkspace workspace)
        {
            IList<AccountTransactionDocumentType> result = new List<AccountTransactionDocumentType>();
            if (values.Length > 0)
            {
                foreach (var item in values)
                {
                    var parts = item.Split(';');
                    if (parts.Count() > 3)
                    {
                        var name = parts[0].Trim();
                        if (workspace.Single<AccountTransactionDocumentType>(x => x.Name.ToLower() == name.ToLower()) != null) continue;

                        var atName = parts[1].Trim();
                        var header = parts[2].Trim();

                        var accTemplate = workspace.Single<AccountType>(x => x.Name.ToLower() == atName.ToLower());
                        if (accTemplate == null)
                        {
                            using (var w = WorkspaceFactory.Create())
                            {
                                accTemplate = new AccountType { Name = atName };
                                w.Add(accTemplate);
                                w.CommitChanges();
                            }
                        }

                        var resultItem = new AccountTransactionDocumentType
                                             {
                                                 Name = name,
                                                 MasterAccountTypeId = accTemplate.Id,
                                                 ButtonHeader = header,
                                                 ButtonColor = "Gainsboro"
                                             };

                        for (var i = 3; i < parts.Length; i++)
                        {
                            var n = parts[i].ToLower();
                            var tt = workspace.Single<AccountTransactionType>(x => x.Name.ToLower() == n);
                            if (tt != null) resultItem.TransactionTypes.Add(tt);
                        }

                        result.Add(resultItem);
                    }
                }
            }
            return result;
        }

        private static decimal ConvertToDecimal(string priceStr, string decimalSeperator)
        {
            try
            {
                priceStr = priceStr.Replace(".", decimalSeperator);
                priceStr = priceStr.Replace(",", decimalSeperator);

                var price = Convert.ToDecimal(priceStr);
                return price;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void CreateDefaultCurrenciesIfNeeded()
        {
            LocalSettings.ReportCurrencyFormat = "#,0.00;(#,0.00);-";
            LocalSettings.ReportQuantityFormat = "0.##;-0.##;-";
            LocalSettings.CurrencyFormat = "#,#0.00";
            LocalSettings.QuantityFormat = "#,#0.##";
            LocalSettings.PrintoutCurrencyFormat = "#,#0.00;-#,#0.00;";
        }



        public static string GetDefaultTicketPrintTemplate()
        {
            const string template = @"[LAYOUT]
-- General layout
<T><%TICKET>
<L00><%DATE>:{TICKET DATE}
<L00><%TIME>:{TIME}
{ENTITIES}
<L00><%TICKET> No:{TICKET NO}
<F>-
{ORDERS}
<F>=
<EB>
{DISCOUNTS}
[<J10><%TOTAL> <%GIFT>:|{ORDER STATE TOTAL:<%GIFT>}]
<J10><%TOTAL>:|{TICKET TOTAL}
{PAYMENTS}
<DB>
<F>=
<C10>T H A N K   Y O U

[DISCOUNTS]
<J00>{CALCULATION NAME} %{CALCULATION AMOUNT}|{CALCULATION TOTAL}

[PAYMENTS]
<J00>{PAYMENT NAME}|{PAYMENT AMOUNT}

[ORDERS]
-- Default format for orders
<J00>- {QUANTITY} {NAME}|{PRICE}
{ORDER TAGS}

[ORDERS:<%GIFT>]
-- Format for gifted orders
<J00>- {QUANTITY} {NAME}|**GIFT**
{ORDER TAGS}

[ORDERS:<%VOID>]
-- Nothing will print for void lines

[ORDER TAGS]
-- Format for order tags
<J00> * {ORDER TAG NAME} | {ORDER TAG PRICE}

[ENTITIES:<%TABLE>]
-- Table entity format
<L00><%TABLE>: {ENTITY NAME}

[ENTITIES:<%CUSTOMER>]
-- Customer entity format
<J00><%CUSTOMER>: {ENTITY NAME} | {ENTITY DATA:<%PHONE>}";
            return ReplaceTemplateValues(template);
        }

        public static string GetDefaultKitchenPrintTemplate()
        {
            const string template = @"[LAYOUT]
<T><%TICKET>
<L00><%DATE>:{TICKET DATE}
<L00><%TIME>:{TIME}
<L00><%TABLE>:{ENTITY NAME:<%TABLE>}
<L00><%TICKET> No:{TICKET NO}
<F>-
{ORDERS}

[ORDERS]
<L00>- {QUANTITY} {NAME}
{ORDER TAGS}

[ORDERS:<%VOID>]
<J00>- {QUANTITY} {NAME}|**<%VOID>**
{ORDER TAGS}

[ORDER TAGS]
-- Format for order tags
<L00>     * {ORDER TAG NAME}";

            return ReplaceTemplateValues(template);
        }

        public static string GetDefaultCustomerReceiptTemplate()
        {
            const string template = @"[LAYOUT]
-- General layout
<T><%RECEIPT>
<L00><%DATE>:{DOCUMENT DATE}
<L00><%TIME>:{DOCUMENT TIME}
<L00>{DESCRIPTION}
<F>-
{TRANSACTIONS}
<F>-

[TRANSACTIONS]
<J00>{SOURCE ACCOUNT} | {AMOUNT}
<J00><%BALANCE>:|{SOURCE BALANCE}";
            return ReplaceTemplateValues(template);
        }

        private static string ReplaceTemplateValues(string template)
        {
            template = template.Replace("<%TICKET>", Resources.Ticket);
            template = template.Replace("<%DATE>", Resources.Date);
            template = template.Replace("<%TIME>", Resources.Time);
            template = template.Replace("<%GIFT>", Resources.Gift);
            template = template.Replace("<%VOID>", Resources.Void);
            template = template.Replace("<%TABLE>", Resources.Table);
            template = template.Replace("<%CUSTOMER>", Resources.Customer);
            template = template.Replace("<%PHONE>", Resources.Phone);
            template = template.Replace("<%TOTAL>", Resources.Total);
            template = template.Replace("<%RECEIPT>", Resources.Receipt);
            template = template.Replace("<%BALANCE>", Resources.Balance);
            return template;
        }
    }
}
