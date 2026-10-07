# The command line

`desktop-driver` reads and drives an application that was started to be driven. One
operation per run: it connects, asks, prints the answer and exits.

The answer is the protocol's own JSON on standard output, and nothing else goes there — so a script can
pipe it straight into a JSON reader without filtering anything out of it first. Errors, refusals and the
usage text all go to standard error.

```sh
dotnet tool install StockSharp.DesktopDriver.Cli --tool-path tools
```

## Reaching an application

```sh
export STOCKSHARP_UI_ENDPOINT=/path/to/endpoint.json

desktop-driver windows
```

`--endpoint` names the same file on the command line. The application must have been started with
`--ui-automation`; its local pipe is restricted to the current OS user.

`--app` says which product is expected to answer, and defaults to whichever one the endpoint names.
`--connect-timeout` is in seconds, 15 by default.

## Exit codes

They are fixed, because a script decides what to do from the number rather than from the message.

| code | meaning | what a script does |
|---|---|---|
| 0 | the operation ran | carry on |
| 1 | something went wrong | look at standard error |
| 2 | the command line is wrong | fix the script |
| 3 | the application could not be reached | start it, or wait and try again |
| 4 | the application refused the operation | the request was wrong, not the moment |

Three and four are never the same number on purpose: the first is worth trying again and the second is
not. A refusal prints the protocol's whole `UiError` — its code is what to branch on, its details say
which node or which action it was about.

**Nought means the operation ran, and nothing more.** A click that was delivered exits nought whether or
not the button did what was hoped. That is a separate question, asked with `snapshot` or `wait`.

## Operations

`desktop-driver help` prints the whole list. One operation per method the protocol has, and
nothing else: a program that turned the word it was given into a call by reflection would run whatever
the protocol grew next, named by whoever wrote the script.

Reading: `session`, `windows`, `find`, `tree`, `snapshot`, `grid-columns`, `grid-rows`, `grid-groups`,
`chart-series`, `chart-points`, `book-levels`, `property-items`, `dock-layout`, `diagnostics`.

Driving: `click`, `type`, `key`, `scroll`, `action-status`, `wait`.

Pictures and files: `screenshot`, `artifact`.

## Naming a node

`--node <scope>/<identifier>`, exactly as `windows`, `find` and `tree` report it. The address survives
everything a user can do to the layout: moving a panel to another dock group, floating it out, and the
visual behind it being rebuilt.

A handle is deliberately not accepted. It only means something inside the session that was given it, and
this program opens a new one every time it runs.

## Worked example

```sh
# What is on screen.
desktop-driver windows

# Find the orders table.
desktop-driver find --kind grid --name Orders

# Read two of its rows.
desktop-driver grid-rows --node "window:MainWindow/OrderGrid" --limit 2

# Click a column header to sort by it, then check that it sorted.
desktop-driver click --node "window:MainWindow/OrderGrid" --part gridHeader --column Price
desktop-driver snapshot --node "window:MainWindow/OrderGrid"

# Reach a row that is not on screen: bring it into view, then look it up again - everything has moved.
desktop-driver show --node "window:MainWindow/OrderGrid" --part gridCell --row row-4210 --column Price
desktop-driver click --node "window:MainWindow/OrderGrid" --part gridCell --row row-4210 --column Price

# Wait for the table to fill up.
desktop-driver wait --node "window:MainWindow/OrderGrid" \
  --field rowCount --op GreaterOrEqual --value 20 --value-kind int64 --timeout 30

# Take a picture of it.
desktop-driver screenshot --node "window:MainWindow/OrderGrid" --out orders.png
```

## Saying what kind a value is

`wait --field` compares against a typed value, and the kind is said rather than guessed:
`--value-kind string|boolean|int64|decimal|double|timestamp|null`, `string` by default.

"100.5" is a price to one caller and a label to another. A wait that compared the wrong one would sit
there until it timed out with nothing to show for it.

## Sending the same input twice

Every input carries an action identifier. The program makes one per run, and `--action <id>` sends a
given one instead. The same identifier twice returns the first receipt rather than clicking again,
which is what a script whose connection broke mid-click needs to be able to do.
