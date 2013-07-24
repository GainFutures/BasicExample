using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OEC.API;
using OEC.Data;
using Account = OEC.API.Account;
using Bar = OEC.API.Bar;
using BaseContract = OEC.API.BaseContract;
using Contract = OEC.API.Contract;
using Currency = OEC.API.Currency;
using Fill = OEC.API.Fill;
using Order = OEC.API.Order;
using Position = OEC.API.Position;
using SymbolLookupCriteria = OEC.API.SymbolLookupCriteria;
using Ticks = OEC.API.Ticks;

namespace OEC.Example
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }


        /// <summary>
        ///     Initiates population of views with data
        /// </summary>
        private void oecClient1_OnLoginComplete()
        {
            UpdateStatus("Logged in");
            LoadContracts();
            RefreshOrders();
            RefreshPositions();
            InitTicket();
        }

        private void LoadContracts()
        {
            foreach (BaseContract bc in oecClient1.ContractGroups["Indices"].BaseContracts)
                if (bc.IsFuture)
                    oecClient1.RequestContracts(bc);

            string[] equities = {"MSFT", "GOOG", "IBM", "OXPS", "AAPL", "SPY", "C"};
            foreach (string eq in equities)
                oecClient1.SymbolLookup(eq);
        }

        /// <summary>
        ///     Usually called when login or password is wrong
        /// </summary>
        /// <param name="reason"></param>
        private void oecClient1_OnLoginFailed(FailReason reason)
        {
            MessageBox.Show(reason.ToString());
        }

        private void oecClient1_OnDisconnected(bool unexpected)
        {
            UpdateStatus("Disconnected");
        }

        /// <summary>
        ///     Connects to OEC Server
        /// </summary>
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                oecClient1.UUID = "9e61a8bc-0a31-4542-ad85-33ebab0e4e86";
                oecClient1.Connect("api.openecry.com", 9200, tbLogin.Text, tbPassword.Text, false);
                UpdateStatus("OEC Server found");
            }
            catch (Exception ex)
            {
                UpdateStatus("Connect failed: " + ex.Message);
            }
        }

        private void UpdateStatus(string text)
        {
            lbStatus.Text = text;
            btnConnect.Enabled = oecClient1.ConnectionClosed;
            btnDisconnect.Enabled = !oecClient1.ConnectionClosed;

            // Note : Orders can be sent only when OECClient.CompleteConnected 
            // flag is set. Client also can wait for OnLoginComplete event, which called
            // at the same time.
            btnSubmit.Enabled = oecClient1.CompleteConnected;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            UpdateStatus("Ready");
        }

        /// <summary>
        ///     Loads simple order descriptions to the list box
        /// </summary>
        private void RefreshOrders()
        {
            lbOrders.Items.Clear();
            lbOrders.Items.AddRange(GetObjects(oecClient1.Orders.Values,
                order => string.Format("{0} {1} {2}, {3} fill(s)", order.ID, order, order.CurrentState,
                    order.Fills.TotalQuantity)
                ));
        }

        /// <summary>
        ///     Converts IList{T} to object[] with user-supplied converter
        /// </summary>
        private object[] GetObjects<T>(IList<T> list, Converter<T, object> converter)
        {
            var array = new T[list.Count];
            list.CopyTo(array, 0);
            object[] objects = Array.ConvertAll(array, converter);
            return objects;
        }

        /// <summary>
        ///     Converts IList{T} to object[] - with no conversion (just typecasting)
        /// </summary>
        private object[] GetObjects<T>(IList<T> list)
        {
            return GetObjects(list, item => item);
        }

        /// <summary>
        ///     Loads positions and balance to the list box
        /// </summary>
        private void RefreshPositions()
        {
            lbPositions.Items.Clear();

            // Most traders have only one account

            Account a = oecClient1.Accounts.First;

            // Position OTE property contains theoretical profit/loss value in USD.
            lbPositions.Items.AddRange(
                GetObjects(a.AvgPositions.Values,
                    p => string.Format("{0} {1} @ {2} P/L {3:C}", p.Contract, p.Net.Volume,
                        p.Contract.PriceToString(p.Net.Price), p.OTE)
                    )
                );

            // TotalBalance property is a sum of all balances for each currency converted to USD.
            // NetLiquidatingValue contains cash balance + open trade equity value (settle PnL), so we need to substract it
            lbPositions.Items.Add(string.Format("Balance : {0:C}",
                a.TotalBalance.NetLiquidatingValue - a.TotalBalance.SettlePnL));
        }

        /// <summary>
        ///     Inits comboboxes in order ticket.
        /// </summary>
        private void InitTicket()
        {
            cbSide.Items.Clear();
            cbSide.Items.AddRange(new object[] {OrderSide.Buy, OrderSide.Sell});

            cbContract.Items.Clear();
            cbContract.Items.AddRange(GetObjects(oecClient1.Contracts.Values));

            cbType.Items.Clear();
            cbType.Items.AddRange(GetObjects((OrderType[]) Enum.GetValues(typeof (OrderType))));
        }

        /// <summary>
        ///     Gathers data from contract to order draft and sends the order.
        /// </summary>
        private void btnSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                OrderDraft d = oecClient1.CreateDraft();

                if (cbSide.SelectedItem != null)
                    d.Side = (OrderSide) cbSide.SelectedItem;

                d.Quantity = (int) nQty.Value;

                if (cbContract.SelectedItem != null)
                {
                    d.Contract = (Contract) cbContract.SelectedItem;
                    var accountType = AccountType.Customer;
                    if (d.Contract.IsEquityAsset)
                        accountType = AccountType.Equity;
                    else if (d.Contract.BaseContract.ContractKind == ContractKind.Forex)
                        accountType = AccountType.StandardFX;
                    foreach (Account account in oecClient1.Accounts)
                    {
                        if (account.Type == accountType)
                        {
                            d.Account = account;
                            break;
                        }
                    }
                }

                if (cbType.SelectedItem != null)
                    d.Type = (OrderType) cbType.SelectedItem;

                d.Price = (double) nPrice.Value;

                // Price2 omitted for simplicity - it is needed only for STPLMT orders

                // validate order draft
                OrderParts res = d.GetInvalidParts();

                if (res != OrderParts.None)
                    throw new Exception(string.Format("Invalid values in {0}", res));

                Console.WriteLine("Sending new order");
                Order order = oecClient1.SendOrder(d);
                Console.WriteLine("Order sent with temp ID: {0}", order.ID);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending order: " + ex.Message);
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            oecClient1.Disconnect();
        }

        /// <summary>
        ///     Price updated only if the contract is currently selected.
        /// </summary>
        private void oecClient1_OnPriceChanged(Contract contract, Price price)
        {
            var selectedContract = cbContract.SelectedItem as Contract;
            if (selectedContract != null && contract.ID == selectedContract.ID)
                UpdatePrice(contract);
        }

        /// <summary>
        ///     Updates current price information
        /// </summary>
        private void UpdatePrice(Contract contract)
        {
            // Contract.PriceToString method formats price according to contract 
            // specifications
            lbPrice.Text = string.Format("Last {0}, Ask {1}, Bid {2}",
                contract.PriceToString(contract.CurrentPrice.LastPrice),
                contract.PriceToString(contract.CurrentPrice.AskPrice),
                contract.PriceToString(contract.CurrentPrice.BidPrice)
                );
        }

        /// <summary>
        ///     When user selects a contract, it displays current price or subscribe for it.
        /// </summary>
        private void cbContract_SelectionChangeCommitted(object sender, EventArgs e)
        {
            var c = cbContract.SelectedItem as Contract;
            if (c != null)
            {
                if (c.CurrentPrice == null)
                    oecClient1.Subscribe(c);
                else
                    UpdatePrice(c);
            }
        }

        /// All those handlers supposed to update single GUI item (position, order or balance),
        /// but for simplification they just refresh related view.
        private void oecClient1_OnAvgPositionChanged(Account account, Position contractPosition)
        {
            RefreshPositions();
        }

        private void oecClient1_OnAccountSummaryChanged(Account account, Currency currency)
        {
            RefreshPositions();
        }

        private void oecClient1_OnBalanceChanged(Account account, Currency currency)
        {
            RefreshPositions();
        }


        private void oecClient1_OnOrderFilled(Order order, Fill fill)
        {
            RefreshOrders();
        }

        private void oecClient1_OnCommandUpdated(Order order, Command command)
        {
            RefreshOrders();
        }

        private void oecClient1_OnOrderConfirmed(Order order, int oldOrderID)
        {
            Console.WriteLine("OnOrderConfirmed: {0} -> {1} ", oldOrderID, order.ID);
            RefreshOrders();
        }


        private void oecClient1_OnOrderStateChanged(Order order, OrderState oldOrderState)
        {
            Console.WriteLine("OnOrderStateChanged: {0}: {2} -> {1} ", order.ID, order.CurrentState, oldOrderState);
            RefreshOrders();
        }

        private void oecClient1_OnContractsChanged(BaseContract bc)
        {
            InitTicket();
        }

        private void oecClient1_OnTicksReceived(Subscription subscription, Ticks ticks)
        {
            Console.WriteLine("{0} : {1} ticks", subscription.Contract, ticks.Prices.Length);
        }

        private void oecClient1_OnBarsReceived(Subscription subscription, Bar[] bars)
        {
            Console.WriteLine("{0} : {1} Bars", subscription.Contract, bars.Length);
        }

        private void oecClient1_OnSymbolLookupReceived(SymbolLookupCriteria symbolLookup, ContractList contracts)
        {
            Console.WriteLine("OnSymbolLookupReceived: {0} : {1} contracts", symbolLookup.SearchText, contracts.Count);
        }
    }
}