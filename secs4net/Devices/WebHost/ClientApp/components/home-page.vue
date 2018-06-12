<template>
    <div>
        <ul>
            <li><strong>Device IP</strong><input type="text" v-model="ip"/></li>
            <li><strong>Port</strong><input type="number" v-model="port"/></li>
            <li><strong>Active</strong><input type="checkbox" v-model="active"/></li>
            <li><button v-on:click="connectToDevice">Connect</button></li>
            <li>Status: <span>{{status}}</span></li>
        </ul>
    </div>
</template>

<script>
import * as signalR from '@aspnet/signalr'

export default {
    data() {
        return {
            ip: '127.0.0.1',
            port:5000,
            active:false,
            status:'N/A',
            connection:null,
        }
    },
    methods: {
        connectToDevice: function () {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(`/secs?ipaddress=${this.ip}&port=${this.port}&active=${this.active}`)
                .configureLogging(signalR.LogLevel.Information)
                .build();
            this.connection.start().catch(err => console.error(err.toString()));
            this.connection.on('ConnectionChanged', status => { 
                this.status = status;
            });
        }
    }
}
</script>

<style>
</style>
