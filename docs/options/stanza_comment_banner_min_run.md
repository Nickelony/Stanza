# stanza_comment_banner_min_run

Controls the fewest repeated marks that make a comment banner, which the comment marker rules leave alone.
Default: `3`.
Read by [`ZA0951`](https://github.com/Nickelony/Stanza/wiki/ZA0951-Separate-a-single-line-comment-marker-from-its-text)
and [`ZA0952`](https://github.com/Nickelony/Stanza/wiki/ZA0952-Separate-a-delimited-comment-marker-from-its-text).

| Value                                           | Effect                                                 |
| ----------------------------------------------- | ------------------------------------------------------ |
| `0`                                             | no run is a banner, so the rules separate every marker |
| a number from 1 to 1000                         | a run of that many repeated marks or more is a banner  |
| below zero, above 1000, absent, or not a number | the default                                            |

At the default, a banner of three marks passes and a shorter run is separated:

```csharp
// Unchanged - one mark repeated three times
//---

// Before - two marks fall short of the default
//--

// After
// --
```

- A single-line comment is a banner when its whole text is one punctuation mark or symbol repeated, such as `//-----`, `//=====` or `//*****`.
  Blanks after the run do not count.
- `//--- Section ---` and `//-=-=-` hold words or more than one mark, so they are not banners.
- A delimited comment keeps each end that a run touches, which reaches from the marker to a blank or to the other marker,
  so `/***** Options *****/` and a box comment are left alone.
