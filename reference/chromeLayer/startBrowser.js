var chromium = require('chrome-aws-lambda');

var startBrowser = async() => {
    var browser = await chromium.puppeteer.launch({
        args: chromium.args,
        defaultViewport: chromium.defaultViewport,
        executablePath: await chromium.executablePath,
        headless: chromium.headless
    });
    console.log('<> wsEndpoint <> wsEndpoint <> wsEndpoint <> wsEndpoint <>');
    console.log(`=browser.wsEndpoint=${browser.wsEndpoint()}=`);
}

return startBrowser().then(() => {
});