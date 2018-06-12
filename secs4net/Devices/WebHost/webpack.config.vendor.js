const path = require('path');
const webpack = require('webpack');
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
var OptimizeCSSPlugin = require('optimize-css-assets-webpack-plugin');

module.exports = {
    stats: { modules: false },
    resolve: {
        extensions: ['.js']
    },
    module: {
        rules: [
            { test: /\.(png|woff|woff2|eot|ttf|svg)(\?|$)/, use: 'url-loader?limit=100000' },
            { test: /\.css(\?|$)/, use: [MiniCssExtractPlugin.loader, 'css-loader'] }
        ]
    },
    entry: {
        vendor: ['bootstrap', 'bootstrap/dist/css/bootstrap.css', 'vue', '@aspnet/signalr', '@aspnet/signalr-protocol-msgpack', 'vuex', 'axios', 'vue-router', 'jquery'],
    },
    output: {
        path: path.join(__dirname, 'wwwroot', 'dist'),
        publicPath: '/dist/',
        filename: '[name].js',
        library: '[name]_[hash]',
    },
    plugins: [
        new MiniCssExtractPlugin('vendor.css'),
        // Compress extracted CSS.
        new OptimizeCSSPlugin({
            cssProcessorOptions: {
                safe: true
            }
        }),
        new webpack.ProvidePlugin({ $: 'jquery', jQuery: 'jquery' }), // Maps these identifiers to the jQuery package (because Bootstrap expects it to be a global variable)
        new webpack.DllPlugin({
            path: path.join(__dirname, 'wwwroot', 'dist', '[name]-manifest.json'),
            name: '[name]_[hash]'
        })
    ]
}