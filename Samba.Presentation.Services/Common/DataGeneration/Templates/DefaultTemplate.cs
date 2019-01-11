using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Entities;
using Samba.Domain.Models.Inventory;
using Samba.Domain.Models.Menus;
using Samba.Domain.Models.Settings;
using Samba.Domain.Models.Tickets;
using Samba.Domain.Models.Users;
using Samba.Infrastructure.Data;
using Samba.Localization.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Samba.Presentation.Services.Common.DataGeneration.Templates
{
    public class DefaultTemplate : IDataTemplate
    {
  
        public void Create(DataCreationService dataCreationService, IWorkspace _workspace)
        {

            var saleAccountType = new AccountType { Name = string.Format(Resources.Accounts_f, Resources.Sales) };
            var receivableAccountType = new AccountType { Name = string.Format(Resources.Accounts_f, Resources.Receiveable) };
            var paymentAccountType = new AccountType { Name = string.Format(Resources.Accounts_f, Resources.Payment) };
            var discountAccountType = new AccountType { Name = string.Format(Resources.Accounts_f, Resources.Discount) };
            var customerAccountType = new AccountType { Name = string.Format(Resources.Accounts_f, Resources.Customer) };

            _workspace.Add(receivableAccountType);
            _workspace.Add(saleAccountType);
            _workspace.Add(paymentAccountType);
            _workspace.Add(discountAccountType);
            _workspace.Add(customerAccountType);
            _workspace.CommitChanges();

            var customerEntityType = new EntityType { Name = Resources.Customers, EntityName = Resources.Customer, AccountTypeId = customerAccountType.Id, PrimaryFieldName = Resources.Name };
            customerEntityType.EntityCustomFields.Add(new EntityCustomField { EditingFormat = "(###) ### ####", FieldType = 0, Name = Resources.Phone });
            customerEntityType.AccountNameTemplate = "[Name]-[" + Resources.Phone + "]";
            var tableEntityType = new EntityType { Name = Resources.Tables, EntityName = Resources.Table, PrimaryFieldName = Resources.Name };

            _workspace.Add(customerEntityType);
            _workspace.Add(tableEntityType);

            _workspace.CommitChanges();

            var accountScreen = new AccountScreen { Name = Resources.General };
            accountScreen.AccountScreenValues.Add(new AccountScreenValue { AccountTypeName = saleAccountType.Name, AccountTypeId = saleAccountType.Id, DisplayDetails = true, SortOrder = 10 });
            accountScreen.AccountScreenValues.Add(new AccountScreenValue { AccountTypeName = receivableAccountType.Name, AccountTypeId = receivableAccountType.Id, DisplayDetails = true, SortOrder = 20 });
            accountScreen.AccountScreenValues.Add(new AccountScreenValue { AccountTypeName = discountAccountType.Name, AccountTypeId = discountAccountType.Id, DisplayDetails = true, SortOrder = 30 });
            accountScreen.AccountScreenValues.Add(new AccountScreenValue { AccountTypeName = paymentAccountType.Name, AccountTypeId = paymentAccountType.Id, DisplayDetails = true, SortOrder = 40 });
            _workspace.Add(accountScreen);

            var defaultSaleAccount = new Account { AccountTypeId = saleAccountType.Id, Name = Resources.Sales };
            var defaultReceivableAccount = new Account { AccountTypeId = receivableAccountType.Id, Name = Resources.Receivables };
            var cashAccount = new Account { AccountTypeId = paymentAccountType.Id, Name = Resources.Cash };
            var creditCardAccount = new Account { AccountTypeId = paymentAccountType.Id, Name = Resources.CreditCard };
            var voucherAccount = new Account { AccountTypeId = paymentAccountType.Id, Name = Resources.Voucher };
            var defaultDiscountAccount = new Account { AccountTypeId = discountAccountType.Id, Name = Resources.Discount };
            var defaultRoundingAccount = new Account { AccountTypeId = discountAccountType.Id, Name = Resources.Rounding };

            _workspace.Add(defaultSaleAccount);
            _workspace.Add(defaultReceivableAccount);
            _workspace.Add(defaultDiscountAccount);
            _workspace.Add(defaultRoundingAccount);
            _workspace.Add(cashAccount);
            _workspace.Add(creditCardAccount);
            _workspace.Add(voucherAccount);

            _workspace.CommitChanges();

            var discountTransactionType = new AccountTransactionType
            {
                Name = string.Format(Resources.Transaction_f, Resources.Discount),
                SourceAccountTypeId = receivableAccountType.Id,
                TargetAccountTypeId = discountAccountType.Id,
                DefaultSourceAccountId = defaultReceivableAccount.Id,
                DefaultTargetAccountId = defaultDiscountAccount.Id
            };

            var roundingTransactionType = new AccountTransactionType
            {
                Name = string.Format(Resources.Transaction_f, Resources.Rounding),
                SourceAccountTypeId = receivableAccountType.Id,
                TargetAccountTypeId = discountAccountType.Id,
                DefaultSourceAccountId = defaultReceivableAccount.Id,
                DefaultTargetAccountId = defaultRoundingAccount.Id
            };

            var saleTransactionType = new AccountTransactionType
            {
                Name = string.Format(Resources.Transaction_f, Resources.Sale),
                SourceAccountTypeId = saleAccountType.Id,
                TargetAccountTypeId = receivableAccountType.Id,
                DefaultSourceAccountId = defaultSaleAccount.Id,
                DefaultTargetAccountId = defaultReceivableAccount.Id
            };

            var paymentTransactionType = new AccountTransactionType
            {
                Name = string.Format(Resources.Transaction_f, Resources.Payment),
                SourceAccountTypeId = receivableAccountType.Id,
                TargetAccountTypeId = paymentAccountType.Id,
                DefaultSourceAccountId = defaultReceivableAccount.Id,
                DefaultTargetAccountId = cashAccount.Id
            };

            var customerAccountTransactionType = new AccountTransactionType
            {
                Name = string.Format(Resources.Customer_f, Resources.AccountTransaction),
                SourceAccountTypeId = receivableAccountType.Id,
                TargetAccountTypeId = customerAccountType.Id,
                DefaultSourceAccountId = defaultReceivableAccount.Id
            };

            var customerCashPaymentType = new AccountTransactionType
            {
                Name = string.Format(Resources.Customer_f, Resources.CashPayment),
                SourceAccountTypeId = customerAccountType.Id,
                TargetAccountTypeId = paymentAccountType.Id,
                DefaultTargetAccountId = cashAccount.Id
            };

            var customerCreditCardPaymentType = new AccountTransactionType
            {
                Name = string.Format(Resources.Customer_f, Resources.CreditCardPayment),
                SourceAccountTypeId = customerAccountType.Id,
                TargetAccountTypeId = paymentAccountType.Id,
                DefaultTargetAccountId = creditCardAccount.Id
            };

            _workspace.Add(saleTransactionType);
            _workspace.Add(paymentTransactionType);
            _workspace.Add(discountTransactionType);
            _workspace.Add(roundingTransactionType);
            _workspace.Add(customerAccountTransactionType);
            _workspace.Add(customerCashPaymentType);
            _workspace.Add(customerCreditCardPaymentType);

            var discountService = new CalculationType
            {
                AccountTransactionType = discountTransactionType,
                CalculationMethod = 0,
                DecreaseAmount = true,
                Name = Resources.Discount
            };

            var roundingService = new CalculationType
            {
                AccountTransactionType = roundingTransactionType,
                CalculationMethod = 2,
                DecreaseAmount = true,
                IncludeTax = true,
                Name = Resources.Round
            };

            var discountSelector = new CalculationSelector { Name = Resources.Discount, ButtonHeader = Resources.DiscountPercentSign };
            discountSelector.CalculationTypes.Add(discountService);
            discountSelector.AddCalculationSelectorMap();

            var roundingSelector = new CalculationSelector { Name = Resources.Round, ButtonHeader = Resources.Round };
            roundingSelector.CalculationTypes.Add(roundingService);
            roundingSelector.AddCalculationSelectorMap();


            _workspace.Add(discountService);
            _workspace.Add(roundingService);
            _workspace.Add(discountSelector);
            _workspace.Add(roundingSelector);

            var screen = new ScreenMenu();
            _workspace.Add(screen);

            var ticketNumerator = new Numerator { Name = Resources.TicketNumerator };
            _workspace.Add(ticketNumerator);

            var orderNumerator = new Numerator { Name = Resources.OrderNumerator };
            _workspace.Add(orderNumerator);




            _workspace.CommitChanges();

            var ticketType = new TicketType
            {
                Name = Resources.Ticket,
                TicketNumerator = ticketNumerator,
                OrderNumerator = orderNumerator,
                SaleTransactionType = saleTransactionType,
                ScreenMenuId = screen.Id,
            };

            ticketType.EntityTypeAssignments.Add(new EntityTypeAssignment { EntityTypeId = tableEntityType.Id, EntityTypeName = tableEntityType.Name, SortOrder = 10 });
            ticketType.EntityTypeAssignments.Add(new EntityTypeAssignment { EntityTypeId = customerEntityType.Id, EntityTypeName = customerEntityType.Name, SortOrder = 20 });

            var cashPayment = new PaymentType
            {
                AccountTransactionType = paymentTransactionType,
                Account = cashAccount,
                Name = cashAccount.Name
            };
            cashPayment.PaymentTypeMaps.Add(new PaymentTypeMap());

            var creditCardPayment = new PaymentType
            {
                AccountTransactionType = paymentTransactionType,
                Account = creditCardAccount,
                Name = creditCardAccount.Name
            };
            creditCardPayment.PaymentTypeMaps.Add(new PaymentTypeMap());

            var voucherPayment = new PaymentType
            {
                AccountTransactionType = paymentTransactionType,
                Account = voucherAccount,
                Name = voucherAccount.Name
            };
            voucherPayment.PaymentTypeMaps.Add(new PaymentTypeMap());

            var accountPayment = new PaymentType
            {
                AccountTransactionType = customerAccountTransactionType,
                Name = Resources.CustomerAccount
            };
            accountPayment.PaymentTypeMaps.Add(new PaymentTypeMap());

            _workspace.Add(cashPayment);
            _workspace.Add(creditCardPayment);
            _workspace.Add(voucherPayment);
            _workspace.Add(accountPayment);
            _workspace.Add(ticketType);

            var warehouseType = new WarehouseType { Name = Resources.Warehouses };
            _workspace.Add(warehouseType);
            _workspace.CommitChanges();

            var localWarehouse = new Warehouse
            {
                Name = Resources.LocalWarehouse,
                WarehouseTypeId = warehouseType.Id
            };

            _workspace.Add(localWarehouse);
            _workspace.CommitChanges();

            var department = new Department
            {
                Name = Resources.Restaurant,
                TicketTypeId = ticketType.Id,
                WarehouseId = localWarehouse.Id
            };
            _workspace.Add(department);

            var transactionType = new InventoryTransactionType
            {
                Name = Resources.PurchaseTransactionType,
                TargetWarehouseTypeId = warehouseType.Id,
                DefaultTargetWarehouseId = localWarehouse.Id
            };

            _workspace.Add(transactionType);

            var transactionDocumentType = new InventoryTransactionDocumentType
            {
                Name = Resources.PurchaseTransaction,
                InventoryTransactionType = transactionType
            };

            _workspace.Add(transactionDocumentType);

            var role = new UserRole("Admin") { IsAdmin = true, DepartmentId = 1 };
            _workspace.Add(role);

            var u = new User("Administrator", "1234") { UserRole = role };
            _workspace.Add(u);

            var ticketPrinterTemplate = new PrinterTemplate { Name = Resources.TicketTemplate, Template = DataCreationService.GetDefaultTicketPrintTemplate() };
            var kitchenPrinterTemplate = new PrinterTemplate { Name = Resources.KitchenOrderTemplate, Template = DataCreationService.GetDefaultKitchenPrintTemplate() };
            var customerReceiptTemplate = new PrinterTemplate { Name = Resources.CustomerReceiptTemplate, Template = DataCreationService.GetDefaultCustomerReceiptTemplate() };

            _workspace.Add(ticketPrinterTemplate);
            _workspace.Add(kitchenPrinterTemplate);
            _workspace.Add(customerReceiptTemplate);

            var printer1 = new Printer { Name = Resources.TicketPrinter };
            var printer2 = new Printer { Name = Resources.KitchenPrinter };
            var printer3 = new Printer { Name = Resources.InvoicePrinter };

            _workspace.Add(printer1);
            _workspace.Add(printer2);
            _workspace.Add(printer3);

            _workspace.CommitChanges();

            var t = new Terminal
            {
                IsDefault = true,
                Name = Resources.Server,
                ReportPrinterId = printer1.Id,
                TransactionPrinterId = printer1.Id,
            };

            var pm1 = new PrinterMap { PrinterId = printer1.Id, PrinterTemplateId = ticketPrinterTemplate.Id };
            _workspace.Add(pm1);

            var pj1 = new PrintJob
            {
                Name = Resources.PrintBill,
                WhatToPrint = (int)WhatToPrintTypes.Everything,
            };
            pj1.PrinterMaps.Add(pm1);


            _workspace.Add(pj1);

            var pm2 = new PrinterMap { PrinterId = printer2.Id, PrinterTemplateId = kitchenPrinterTemplate.Id };
            var pj2 = new PrintJob
            {
                Name = Resources.PrintOrdersToKitchenPrinter,
                WhatToPrint = (int)WhatToPrintTypes.Everything,
            };

            pj2.PrinterMaps.Add(pm2);

            _workspace.Add(pj2);
            _workspace.Add(t);

            new RuleGenerator().GenerateSystemRules(_workspace);

            dataCreationService.ImportMenus(screen);
            dataCreationService.ImportTableResources(tableEntityType, ticketType);

            var customerScreen = new EntityScreen { Name = string.Format(Resources.Customer_f, Resources.Search), DisplayMode = 1, EntityTypeId = customerEntityType.Id, TicketTypeId = ticketType.Id };
            customerScreen.EntityScreenMaps.Add(new EntityScreenMap());
            _workspace.Add(customerScreen);

            var customerTicketScreen = new EntityScreen { Name = Resources.CustomerTickets, DisplayMode = 0, EntityTypeId = customerEntityType.Id, StateFilter = Resources.NewOrders, ColumnCount = 6, RowCount = 6, TicketTypeId = ticketType.Id };
            customerTicketScreen.EntityScreenMaps.Add(new EntityScreenMap());
            _workspace.Add(customerTicketScreen);

            var customerCashDocument = new AccountTransactionDocumentType
            {
                Name = string.Format(Resources.Customer_f, Resources.Cash),
                ButtonHeader = Resources.Cash,
                DefaultAmount = string.Format("[{0}]", Resources.Balance),
                DescriptionTemplate = string.Format(Resources.Payment_f, Resources.Cash),
                MasterAccountTypeId = customerAccountType.Id,
                PrinterTemplateId = customerReceiptTemplate.Id
            };
            customerCashDocument.AddAccountTransactionDocumentTypeMap();
            customerCashDocument.TransactionTypes.Add(customerCashPaymentType);

            var customerCreditCardDocument = new AccountTransactionDocumentType
            {
                Name = string.Format(Resources.Customer_f, Resources.CreditCard),
                ButtonHeader = Resources.CreditCard,
                DefaultAmount = string.Format("[{0}]", Resources.Balance),
                DescriptionTemplate = string.Format(Resources.Payment_f, Resources.CreditCard),
                MasterAccountTypeId = customerAccountType.Id,
                PrinterTemplateId = customerReceiptTemplate.Id
            };
            customerCreditCardDocument.AddAccountTransactionDocumentTypeMap();
            customerCreditCardDocument.TransactionTypes.Add(customerCreditCardPaymentType);

            _workspace.Add(customerCashDocument);
            _workspace.Add(customerCreditCardDocument);


        }
    }
}
