# stanza_markup_enabled

Controls whether a markup file takes part in a run of [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md).
Default: `true`.
Read by stanza-markup.

| Value         | Effect                                                        |
| ------------- | ------------------------------------------------------------- |
| `true`        | the file is laid out like every other file its profile claims |
| `false`       | the file is taken out of the run                              |
| anything else | the file stays in the run, and the tool reports a warning     |

A section glob that ends in `/**` takes every file under that directory out of the run.
This example skips a third-party directory and every directory named `Generated`:

```ini
[3rdParty/**]
stanza_markup_enabled = false

[**/Generated/**]
stanza_markup_enabled = false
```

A file named on the command line that this key disables is skipped with a [note](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#failures-and-warnings).
