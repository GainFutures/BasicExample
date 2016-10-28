OEC API Example

Note - in order to run this program, you need to obtain a valid username and password for 
OEC simulation server (api.gainfutures.com).

This program demostrates basic principles of operation of OEC API. A single form contains the 
OECClient component instance with some event handlers hooked to it, and GUI views. The form is 
divided by 4 areas - login, positions, orders and order entry.

In the login pane user can enter username and password, and then connect to OEC Server. Current
connection status is displayed in a label below the Connect/Disconnect buttons and updated when
API raises one of connection-related events.
( see Form1.UpdateStatus )

After successful connection views will be populated with API data, if available. 
( see Form1.OnLoginComplete )

Order view is a list box, where all orders of current session are displayed. Each row reflects a single 
order : it's ID, order description and number of fills received. The list is updated each time
when any order is changed.
( see Form1.RefreshOrders )

Position list box is populated with open position information, including symbol, number of contracts
(positive for long positions and negative for short), average price and theoretical profit/loss.
Last item reflects account cash balance.
( see Form1.RefreshPositions )

In the order entry pane user can select side (buy/sell), quantity, contract and order type (Market, Limit etc);
and then submit a new order. If required order information is missing, an error message will be displayed.
( see Form1.btnSubmit_Click )

OEC API assembly (app.dll) and its XML documentation (api.xml) are located in the ".\API" folder.