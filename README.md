# Helix

Helix is a free, Apache licensed website crawler. It systematically and recursively browses a website using a web browser to fully render each URL it visits and capture all the resources (videos, audios, pdf ...) which are downloaded in the rendering process. Helix is built using .NET Core framework and, right now, only supports Windows.

**Download**: *(coming soon)*  
**Demo video**: [https://youtu.be/0RWHfZj5sZA](https://www.youtube.com/watch?v=0RWHfZj5sZA)

# Screenshots

*(Coming soon)*

# Use cases

Helix can provide support in various scenarios:

 - **Estimate how big a website is**  
Use Helix to scan the website then count the number of `internal URLs` in the report. That number will give you an estimate of how big a website is.
 - **Locate broken URLs in a website**  
Use Helix to scan the website then filter out URLs having status code greater than or equal to 400, or less than 0.
 - **List out redirects**  
Use Helix to scan the website then filter out 3xx URLs.
 - **Estimate average page load time of a website**  
Use Helix to scan the website then look at the `Avg. Page Load Time` number.

# Features

 - Recursive and Multi-threaded
 - Use web browser to fully render website
 - Capable of capturing all network traffic between the web browser and web server
	 - This feature allows Helix to capture every image loaded by a website. Even if they are used from `CSS`
 - Support `SQLite` and `CSV` outputs
 - Support using `SQL` to perform complex queries against `SQLite` output
 - Provide screenshot evidence of broken URLs
 - Graphical User Interface

# Usage

Just download the latest release of Helix and watch the [demo video](https://youtu.be/0RWHfZj5sZA). Currently, Helix can only run on Windows.
