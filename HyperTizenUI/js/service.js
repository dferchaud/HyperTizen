// Runs inside TizenBrew's Node.js 4 sandbox: ES5 only (no arrow functions, let/const, template strings).
var http = require('http');

var SERVICE_APP_ID = 'io.gh.reisxd.HyperTizen';
var STATUS_PORT = 8087;
var SERVICE_CHECK_URL = { host: '127.0.0.1', port: 8086, path: '/logs', timeout: 1500 };
var RETRY_MS = 5000;
var MAX_ATTEMPTS = 24;

var status = {
    node: typeof process !== 'undefined' ? process.version : 'inconnu',
    tizenApi: typeof tizen !== 'undefined' && tizen && tizen.application ? 'oui' : 'non',
    serviceUp: false,
    attempts: 0,
    launching: false,
    log: []
};

function note(message) {
    status.log.push(new Date().toISOString() + ' ' + message);
    while (status.log.length > 40) status.log.shift();
    try { console.log('[HyperTizen service] ' + message); } catch (e) { }
}

function checkServiceUp(callback) {
    var finished = false;
    function done(up) {
        if (finished) return;
        finished = true;
        callback(up);
    }
    try {
        var req = http.get(SERVICE_CHECK_URL, function (res) {
            res.resume();
            done(res.statusCode === 200);
        });
        req.on('error', function () { done(false); });
        req.setTimeout(SERVICE_CHECK_URL.timeout, function () {
            try { req.abort(); } catch (e) { }
            done(false);
        });
    } catch (e) {
        done(false);
    }
}

function launchViaAppControl(onFailure) {
    try {
        tizen.application.launchAppControl(
            new tizen.ApplicationControl('http://tizen.org/appcontrol/operation/service'),
            SERVICE_APP_ID,
            function () { note('launchAppControl(service) accepte'); },
            function (e) {
                note('launchAppControl(service) refuse: ' + (e && e.message ? e.message : e));
                onFailure();
            }
        );
    } catch (e) {
        note('launchAppControl(service) exception: ' + e.message);
        onFailure();
    }
}

function launchViaLaunch() {
    try {
        tizen.application.launch(
            SERVICE_APP_ID,
            function () { note('launch() accepte'); },
            function (e) { note('launch() refuse: ' + (e && e.message ? e.message : e)); }
        );
    } catch (e) {
        note('launch() exception: ' + e.message);
    }
}

function tryLaunch(reason) {
    if (status.launching) return;
    status.launching = true;
    checkServiceUp(function (up) {
        status.serviceUp = up;
        if (up) {
            note('service deja actif (' + reason + ')');
            status.launching = false;
            return;
        }
        status.attempts++;
        if (typeof tizen === 'undefined' || !tizen || !tizen.application) {
            note('tizen.application indisponible dans ce bac a sable: impossible de lancer le service');
            status.launching = false;
            return;
        }
        note('lancement du service (' + reason + ', essai ' + status.attempts + ')');
        launchViaAppControl(launchViaLaunch);
        setTimeout(function () { status.launching = false; }, 2000);
    });
}

function send(res, code, body) {
    res.writeHead(code, {
        'Content-Type': 'application/json; charset=utf-8',
        'Access-Control-Allow-Origin': '*'
    });
    res.end(JSON.stringify(body));
}

try {
    var server = http.createServer(function (req, res) {
        if (req.url === '/launch') {
            tryLaunch('demande manuelle');
            setTimeout(function () { send(res, 200, status); }, 2500);
        } else {
            send(res, 200, status);
        }
    });
    server.on('error', function (e) { note('serveur etat indisponible: ' + e.message); });
    server.listen(STATUS_PORT, function () { note('etat sur le port ' + STATUS_PORT + ' (/status, /launch)'); });
} catch (e) {
    note('serveur etat impossible: ' + e.message);
}

note('demarrage: node ' + status.node + ', tizen.application ' + status.tizenApi);
tryLaunch('demarrage');

var retries = 0;
var retryTimer = setInterval(function () {
    retries++;
    if (status.serviceUp || retries >= MAX_ATTEMPTS) {
        clearInterval(retryTimer);
        return;
    }
    tryLaunch('nouvel essai');
}, RETRY_MS);
