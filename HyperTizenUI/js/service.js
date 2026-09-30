var attempts = 0;
var MAX_ATTEMPTS = 60;

var interval = setInterval(function () {
    attempts++;
    if (attempts > MAX_ATTEMPTS) {
        clearInterval(interval);
        return;
    }
    try {
        tizen.application.getAppInfo('io.gh.reisxd.HyperTizen');
        tizen.application.launch(
            'io.gh.reisxd.HyperTizen',
            function () {
                console.log('Launch Service succeeded');
                clearInterval(interval);
            },
            function (e) {
                console.log('Launch Service failed: ' + e.message);
            }
        );
    } catch (e) {
        console.log('App not found');
    }
}, 1000);
