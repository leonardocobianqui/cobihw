# Privacy

cobihw doesn't collect or send any personal data. It has no telemetry, no analytics, no ads and no account.

## What stays on your computer

- **Hardware readings** are read from Windows and shown on the screen. They aren't saved, except the network totals of the current session and the result of the last speed test, which are kept in the settings file.
- **Settings** are saved in `cobihw.settings.json`, in the same folder as `cobihw.exe`. The file holds one section per computer and Windows user, identified by the computer name, the Windows user name and the user's security identifier (SID), so several PCs can share one portable folder. You can remove the other sections in **Settings → General**.
- **Windows entries:** when you turn them on, cobihw adds a "Start with Windows" entry to your user's registry and an "FPS" scheduled task. **Settings → General → Remove cobihw from Windows** deletes both.

## When cobihw goes online

cobihw only connects to the internet for features you start. Nothing is sent in the background.

| Feature | When | Who receives the request | What is sent |
| --- | --- | --- | --- |
| Internet speed test | When you start a test | Cloudflare (`speed.cloudflare.com`) | Test data to download and upload |
| Public IP | When you click **Discover** in Network devices | Cloudflare (`www.cloudflare.com/cdn-cgi/trace`) | A plain request; Cloudflare answers with the address it sees |
| Update check | When you click **Check for updates** in About | GitHub (`api.github.com`) | A request for the latest release |
| Ping | Every 5 seconds while ping is on (off by default) | The address you choose (8.8.8.8 by default) | A standard ICMP ping |

Like any connection, these requests reveal your IP address to the server that receives them. Their privacy policies apply: [Cloudflare](https://www.cloudflare.com/privacypolicy/), [GitHub](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement), and, for the default ping address, [Google Public DNS](https://developers.google.com/speed/public-dns/privacy).

The **Website** and **GitHub** buttons in About open those pages in your browser; cobihw itself doesn't contact them.

## Questions

Open an [issue](https://github.com/leonardocobianqui/cobihw/issues).
