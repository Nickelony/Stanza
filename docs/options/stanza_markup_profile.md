# stanza_markup_profile

Controls the layout profile a markup file uses when extension-based selection is not enough.
Default: the profile that claims the file's extension.
Read by stanza-markup.

| Value                    | Effect                                                                |
| ------------------------ | --------------------------------------------------------------------- |
| a profile name, any case | the file is laid out with that profile, whatever the file is called   |
| anything else            | the profile that claims its extension, and the tool reports a warning |
| absent                   | the profile that claims its extension                                 |

- The [profiles table](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) lists the profiles,
  the extensions each claims and the layout policy each one applies.
- A profile's own scope is the default that [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) falls back to when a file sets none.
- `--profile` keeps the profile's own scope, so `stanza_markup_scope` does not apply to the files it lays out.
- Naming a file on the command line lays it out with the profile this key sets,
  even when no profile claims its extension.
- A directory scan collects only the files that a profile claims by extension,
  or that a matching section names a profile for.
- `--profile` overrides this key for every collected file,
  and it narrows a directory scan to the files that profile claims by extension.
- Under `--profile`, an invalid value of this key still warns.

To include another extension, name a profile for it in a matching section:

```ini
[*.template]
stanza_markup_profile = xml
```
